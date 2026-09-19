using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/sales")]
    public class TenantSalesTransactionsController : ControllerBase
    {
        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantSalesTransactionsController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetSalesTransactions(int companyId)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transactions = await tenantDb.SalesTransactions
                .AsNoTracking()
                .OrderBy(x => x.TransactionId)
                .ToListAsync();

            return Ok(transactions);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetSalesTransactionById(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transaction = await tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => x.TransactionId == id)
                .Select(x => new
                {
                    x.TransactionId,
                    x.CustomerId,
                    x.PromotionId,
                    x.TransactionDate,
                    x.TotalAmount,
                    x.DiscountAmount,
                    x.PointsUsed,
                    x.PointsEarned,
                    x.FinalAmount,
                    x.PaymentMethod,
                    x.Status,
                    TransactionItems = x.TransactionItems.Select(item => new
                    {
                        item.TransactionItemId,
                        item.ProductId,
                        item.Quantity,
                        item.UnitPrice,
                        item.Subtotal
                    })
                })
                .FirstOrDefaultAsync();

            if (transaction == null)
            {
                return NotFound($"SalesTransaction with id {id} not found.");
            }

            return Ok(transaction);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSalesTransaction(int companyId, SalesTransaction transaction)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers
                .FirstOrDefaultAsync(x => x.CustomerId == transaction.CustomerId);

            if (customer == null)
            {
                return BadRequest($"CustomerId {transaction.CustomerId} does not exist for this tenant.");
            }

            // New transactions can only be created for an Active customer.
            // Historical transactions already on file for a since-deactivated
            // customer are untouched by this check.
            if (!string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Customer \"{customer.FirstName} {customer.LastName}\" is inactive and cannot make new purchases.");
            }

            if (transaction.PromotionId.HasValue)
            {
                var promotion = await tenantDb.Promotions
                    .FirstOrDefaultAsync(x => x.PromotionId == transaction.PromotionId.Value);

                if (promotion == null)
                {
                    return BadRequest($"PromotionId {transaction.PromotionId} does not exist for this tenant.");
                }

                string? promotionError = ValidatePromotion(promotion, transaction.TransactionDate, transaction.TotalAmount);
                if (promotionError != null)
                {
                    return BadRequest(promotionError);
                }

                // Points-based promotions are gated on the customer's current
                // balance, and PointsUsed is always server-derived from the
                // promotion — a client-supplied PointsUsed is never trusted.
                if (promotion.RequiredLoyaltyPoints > 0)
                {
                    if (customer.LoyaltyPoints < promotion.RequiredLoyaltyPoints)
                    {
                        return BadRequest(
                            $"Customer does not have enough loyalty points for this promotion. " +
                            $"Required: {promotion.RequiredLoyaltyPoints}. Available: {customer.LoyaltyPoints}.");
                    }

                    transaction.PointsUsed = promotion.RequiredLoyaltyPoints;
                }
                else
                {
                    transaction.PointsUsed = 0;
                }

                transaction.DiscountAmount = ComputeDiscount(transaction.TotalAmount, promotion, transaction.TransactionDate);
            }
            else
            {
                transaction.PointsUsed = 0;
                transaction.DiscountAmount = 0;
            }

            transaction.FinalAmount = transaction.TotalAmount - transaction.DiscountAmount;

            // New transactions are always Completed; staff never choose the
            // status at creation time (Cancelled only applies to an existing
            // transaction through the update/cancellation flow below).
            transaction.Status = "Completed";

            tenantDb.SalesTransactions.Add(transaction);
            await tenantDb.SaveChangesAsync();

            if (transaction.PointsEarned != 0 || transaction.PointsUsed != 0)
            {
                customer.LoyaltyPoints += transaction.PointsEarned - transaction.PointsUsed;

                var loyaltyTransaction = new LoyaltyTransaction
                {
                    CustomerId = transaction.CustomerId,
                    SalesTransactionId = transaction.TransactionId,
                    PointsEarned = transaction.PointsEarned,
                    PointsUsed = transaction.PointsUsed,
                    TransactionType = "Sale",
                    Date = transaction.TransactionDate
                };

                tenantDb.LoyaltyTransactions.Add(loyaltyTransaction);
                await tenantDb.SaveChangesAsync();
            }

            return Created(
                $"api/tenant/{companyId}/sales/{transaction.TransactionId}",
                transaction);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateSalesTransaction(int companyId, int id, SalesTransaction updated)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transaction = await tenantDb.SalesTransactions
                .FirstOrDefaultAsync(x => x.TransactionId == id);

            if (transaction == null)
            {
                return NotFound($"SalesTransaction with id {id} not found.");
            }

            var customer = await tenantDb.Customers
                .FirstOrDefaultAsync(x => x.CustomerId == transaction.CustomerId);

            if (customer == null)
            {
                return BadRequest("Linked customer no longer exists.");
            }

            bool isAlreadyCancelled = string.Equals(transaction.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
            bool isBeingCancelledNow =
                string.Equals(updated.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                !isAlreadyCancelled;

            // ----- Cancellation branch -----
            if (isBeingCancelledNow)
            {
                // Guard checked BEFORE any mutation. Nothing below this point
                // runs more than once for the same transaction, so inventory
                // and loyalty points can never be reversed twice.
                if (isAlreadyCancelled)
                {
                    return Conflict("This transaction is already cancelled and cannot be modified further.");
                }

                // 1. Restore inventory for every item this sale consumed.
                var items = await tenantDb.TransactionItems
                    .Where(x => x.TransactionId == id)
                    .ToListAsync();

                foreach (var item in items)
                {
                    var product = await tenantDb.Products
                        .FirstOrDefaultAsync(x => x.ProductId == item.ProductId);

                    if (product != null)
                    {
                        product.Quantity += item.Quantity;
                    }
                }

                // 2. Reverse whatever loyalty effect the original sale had.
                // Works identically whether PointsEarned/PointsUsed are 0 or not.
                int originalDelta = transaction.PointsEarned - transaction.PointsUsed;
                customer.LoyaltyPoints -= originalDelta;

                // 3. Record the reversal for audit history, but only when the
                // original sale actually had a loyalty effect to reverse. The
                // original "Sale" LoyaltyTransaction record is left untouched.
                if (transaction.PointsEarned != 0 || transaction.PointsUsed != 0)
                {
                    tenantDb.LoyaltyTransactions.Add(new LoyaltyTransaction
                    {
                        CustomerId = transaction.CustomerId,
                        SalesTransactionId = transaction.TransactionId,
                        PointsEarned = transaction.PointsUsed,
                        PointsUsed = transaction.PointsEarned,
                        TransactionType = "Cancellation",
                        Date = DateTime.UtcNow
                    });
                }

                // 4. Cancellation only ever changes Status. It must not touch
                // PromotionId, TotalAmount, PaymentMethod, etc. — that would be
                // indistinguishable from creating another normal completed sale.
                transaction.Status = "Cancelled";

                await tenantDb.SaveChangesAsync();

                return Ok(ToFlatResponse(transaction));
            }

            // ----- Normal update branch -----

            // A cancelled transaction is a closed historical record. The only
            // way back is not supported — cancellation is a one-way correction.
            if (isAlreadyCancelled)
            {
                return Conflict("This transaction is already cancelled and cannot be modified further.");
            }

            decimal discountAmount = 0;

            if (updated.PromotionId.HasValue)
            {
                var promotion = await tenantDb.Promotions
                    .FirstOrDefaultAsync(x => x.PromotionId == updated.PromotionId.Value);

                if (promotion == null)
                {
                    return BadRequest($"PromotionId {updated.PromotionId} does not exist for this tenant.");
                }

                string? promotionError = ValidatePromotion(promotion, updated.TransactionDate, updated.TotalAmount);
                if (promotionError != null)
                {
                    return BadRequest(promotionError);
                }

                // Same server-controlled PointsUsed rule as Create: a client
                // cannot attach a points-based promotion to an existing
                // transaction and supply its own PointsUsed value.
                if (promotion.RequiredLoyaltyPoints > 0)
                {
                    // The points this transaction already has reserved are
                    // still spendable by it (CustomerId cannot change via
                    // update, so this is always the same customer).
                    int availablePoints = customer.LoyaltyPoints + transaction.PointsUsed;

                    if (availablePoints < promotion.RequiredLoyaltyPoints)
                    {
                        return BadRequest(
                            $"Customer does not have enough loyalty points for this promotion. " +
                            $"Required: {promotion.RequiredLoyaltyPoints}. Available: {availablePoints}.");
                    }

                    updated.PointsUsed = promotion.RequiredLoyaltyPoints;
                }
                else
                {
                    updated.PointsUsed = 0;
                }

                discountAmount = ComputeDiscount(updated.TotalAmount, promotion, updated.TransactionDate);
            }
            else
            {
                updated.PointsUsed = 0;
            }

            int oldDelta = transaction.PointsEarned - transaction.PointsUsed;
            int newDelta = updated.PointsEarned - updated.PointsUsed;
            customer.LoyaltyPoints += (newDelta - oldDelta);

            var linkedLoyalty = await tenantDb.LoyaltyTransactions
                .FirstOrDefaultAsync(x => x.SalesTransactionId == id && x.TransactionType == "Sale");

            if (linkedLoyalty != null)
            {
                linkedLoyalty.PointsEarned = updated.PointsEarned;
                linkedLoyalty.PointsUsed = updated.PointsUsed;
                linkedLoyalty.Date = updated.TransactionDate;
            }
            else if (updated.PointsEarned != 0 || updated.PointsUsed != 0)
            {
                tenantDb.LoyaltyTransactions.Add(new LoyaltyTransaction
                {
                    CustomerId = transaction.CustomerId,
                    SalesTransactionId = transaction.TransactionId,
                    PointsEarned = updated.PointsEarned,
                    PointsUsed = updated.PointsUsed,
                    TransactionType = "Sale",
                    Date = updated.TransactionDate
                });
            }

            transaction.PromotionId = updated.PromotionId;
            transaction.TransactionDate = updated.TransactionDate;
            transaction.TotalAmount = updated.TotalAmount;
            transaction.DiscountAmount = discountAmount;
            transaction.PointsUsed = updated.PointsUsed;
            transaction.PointsEarned = updated.PointsEarned;
            transaction.FinalAmount = updated.TotalAmount - discountAmount;
            transaction.PaymentMethod = updated.PaymentMethod;
            transaction.Status = updated.Status;

            await tenantDb.SaveChangesAsync();

            return Ok(ToFlatResponse(transaction));
        }

        // Sales Transactions are historical records and are never hard-deleted.
        // Cancellation (PUT with Status = "Cancelled") is the only correction
        // mechanism, so the DELETE endpoint has been removed.

        private static string? ValidatePromotion(Promotion promotion, DateTime transactionDate, decimal totalAmount)
        {
            if (!string.Equals(promotion.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return $"Promotion \"{promotion.PromotionName}\" is not active.";
            }

            if (transactionDate < promotion.StartDate || transactionDate > promotion.EndDate)
            {
                return $"Promotion \"{promotion.PromotionName}\" is not valid on {transactionDate:MM/dd/yyyy}.";
            }

            if (totalAmount < promotion.MinimumPurchase)
            {
                return $"Promotion \"{promotion.PromotionName}\" requires a minimum purchase of {promotion.MinimumPurchase:C2}.";
            }

            return null;
        }

        private static decimal ComputeDiscount(decimal totalAmount, Promotion promotion, DateTime transactionDate)
        {
            if (!string.Equals(promotion.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (transactionDate < promotion.StartDate || transactionDate > promotion.EndDate)
            {
                return 0;
            }

            if (totalAmount < promotion.MinimumPurchase)
            {
                return 0;
            }

            decimal discount = string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                ? totalAmount * (promotion.DiscountValue / 100m)
                : promotion.DiscountValue;

            if (discount > totalAmount)
            {
                discount = totalAmount;
            }

            return discount;
        }

        private static object ToFlatResponse(SalesTransaction transaction)
        {
            return new
            {
                transaction.TransactionId,
                transaction.CustomerId,
                transaction.PromotionId,
                transaction.TransactionDate,
                transaction.TotalAmount,
                transaction.DiscountAmount,
                transaction.PointsUsed,
                transaction.PointsEarned,
                transaction.FinalAmount,
                transaction.PaymentMethod,
                transaction.Status
            };
        }
    }
}