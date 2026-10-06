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
        // Loyalty earning rule: PHP 10 of FinalAmount = 1 point (rounded down), max 100 points per sale.
        private const decimal LoyaltyAmountPerPoint = 10m;
        private const int MaxPointsEarnedPerSale = 100;

        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantSalesTransactionsController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetSalesTransactions(int companyId, bool includeDeleted = false)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var query = tenantDb.SalesTransactions.AsNoTracking();

            if (!includeDeleted)
            {
                query = query.Where(x => !x.IsDeleted);
            }

            var transactions = await query
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
                    x.CustomerDiscountAmount,
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

            if (!string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Customer \"{customer.FirstName} {customer.LastName}\" is inactive and cannot make new purchases.");
            }

            Promotion? appliedPromotion = null;

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

                string? eligibilityError = await ValidateEligibilityAsync(tenantDb, promotion, customer.CustomerId);
                if (eligibilityError != null)
                {
                    return BadRequest(eligibilityError);
                }

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
                appliedPromotion = promotion;
            }
            else
            {
                transaction.PointsUsed = 0;
                transaction.DiscountAmount = 0;
            }

            transaction.CustomerDiscountAmount = await ComputeCustomerDiscountAsync(
                tenantDb,
                customer.CustomerId,
                transaction.TotalAmount - transaction.DiscountAmount,
                appliedPromotion);

            transaction.FinalAmount = transaction.TotalAmount - transaction.DiscountAmount - transaction.CustomerDiscountAmount;

            // Never trust a client-supplied value.
            transaction.PointsEarned = CalculatePointsEarned(transaction.FinalAmount);

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

            if (isBeingCancelledNow)
            {
                if (isAlreadyCancelled)
                {
                    return Conflict("This transaction is already cancelled and cannot be modified further.");
                }
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

                int originalDelta = transaction.PointsEarned - transaction.PointsUsed;
                customer.LoyaltyPoints -= originalDelta;

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

                transaction.Status = "Cancelled";

                await tenantDb.SaveChangesAsync();

                return Ok(ToFlatResponse(transaction));
            }

            if (isAlreadyCancelled)
            {
                return Conflict("This transaction is already cancelled and cannot be modified further.");
            }

            decimal discountAmount = 0;
            Promotion? appliedPromotion = null;

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

                string? eligibilityError = await ValidateEligibilityAsync(tenantDb, promotion, customer.CustomerId);
                if (eligibilityError != null)
                {
                    return BadRequest(eligibilityError);
                }

                if (promotion.RequiredLoyaltyPoints > 0)
                {
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
                appliedPromotion = promotion;
            }
            else
            {
                updated.PointsUsed = 0;
            }

            decimal customerDiscountAmount =
                updated.PromotionId == transaction.PromotionId && updated.TotalAmount == transaction.TotalAmount
                    ? Math.Min(transaction.CustomerDiscountAmount, Math.Max(updated.TotalAmount - discountAmount, 0m))
                    : await ComputeCustomerDiscountAsync(
                        tenantDb,
                        customer.CustomerId,
                        updated.TotalAmount - discountAmount,
                        appliedPromotion);

            decimal finalAmount = updated.TotalAmount - discountAmount - customerDiscountAmount;

            // Never trust a client-supplied value.
            updated.PointsEarned = CalculatePointsEarned(finalAmount);

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
            transaction.CustomerDiscountAmount = customerDiscountAmount;
            transaction.PointsUsed = updated.PointsUsed;
            transaction.PointsEarned = updated.PointsEarned;
            transaction.FinalAmount = finalAmount;
            transaction.PaymentMethod = updated.PaymentMethod;
            transaction.Status = updated.Status;

            await tenantDb.SaveChangesAsync();

            return Ok(ToFlatResponse(transaction));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteSalesTransaction(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transaction = await tenantDb.SalesTransactions
                .FirstOrDefaultAsync(x => x.TransactionId == id);

            if (transaction == null)
            {
                return NotFound($"SalesTransaction with id {id} not found.");
            }

            if (string.Equals(transaction.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("A completed transaction cannot be deleted. Cancel it first so its stock and loyalty points are restored.");
            }

            transaction.IsDeleted = true;
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }

        private static async Task<string?> ValidateEligibilityAsync(
            freshcrumbs.CRM.infrastructure.data.TenantCrmDbContext tenantDb,
            Promotion promotion,
            int customerId)
        {
            if (string.IsNullOrWhiteSpace(promotion.EligibilityCategory))
            {
                return null;
            }

            bool isVerifiedForCategory = await tenantDb.CustomerDiscountEligibilities.AnyAsync(x =>
                x.CustomerId == customerId &&
                x.Category == promotion.EligibilityCategory &&
                x.VerificationStatus == "Verified");

            if (!isVerifiedForCategory)
            {
                return $"Promotion \"{promotion.PromotionName}\" requires verified {promotion.EligibilityCategory} eligibility for this customer.";
            }

            return null;
        }

        private static async Task<decimal> ComputeCustomerDiscountAsync(
            freshcrumbs.CRM.infrastructure.data.TenantCrmDbContext tenantDb,
            int customerId,
            decimal amountAfterPromotion,
            Promotion? appliedPromotion)
        {
            if (amountAfterPromotion <= 0)
            {
                return 0;
            }

            if (appliedPromotion != null && !string.IsNullOrWhiteSpace(appliedPromotion.EligibilityCategory))
            {
                return 0;
            }

            var verifiedCategories = await tenantDb.CustomerDiscountEligibilities
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId && x.VerificationStatus == "Verified")
                .Select(x => x.Category)
                .ToListAsync();

            decimal rate = verifiedCategories
                .Select(GetCustomerDiscountRate)
                .DefaultIfEmpty(0m)
                .Max();

            if (rate <= 0)
            {
                return 0;
            }

            return Math.Round(amountAfterPromotion * rate, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal GetCustomerDiscountRate(string category)
        {
            return category switch
            {
                "Senior Citizen" => 0.20m,
                "PWD" => 0.20m,
                _ => 0m
            };
        }

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

        private static int CalculatePointsEarned(decimal finalAmount)
        {
            if (finalAmount <= 0m)
            {
                return 0;
            }

            decimal points = Math.Floor(finalAmount / LoyaltyAmountPerPoint);
            return (int)Math.Min(points, MaxPointsEarnedPerSale);
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
                transaction.CustomerDiscountAmount,
                transaction.PointsUsed,
                transaction.PointsEarned,
                transaction.FinalAmount,
                transaction.PaymentMethod,
                transaction.Status
            };
        }
    }
}