using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
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

        private ObjectResult FeatureNotIncluded(string feature)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "FeatureNotIncluded",
                message = $"Your current plan does not include {feature}."
            });
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

            // PREMIUM MANAGER / STAFF: their assigned branch only (from the account, never from the request).
            var scope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);

            if (scope.Restricted)
            {
                query = query.Where(x => x.BranchId != null && x.BranchId == scope.BranchId);
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
                    x.BranchId,
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

            if (!(await BranchStock.GetScopeAsync(HttpContext, tenantDb)).Allows(transaction.BranchId))
            {
                return OtherBranch();
            }

            return Ok(transaction);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSalesTransaction(int companyId, SalesTransaction transaction)
        {
            string? fieldError = ValidateSaleFields(transaction, null);
            if (fieldError != null)
            {
                return BadRequest(InputRules.Message(fieldError));
            }

            // Linked records are looked up by id only; objects nested in the request are never saved.
            transaction.Customer = null;
            transaction.Promotion = null;

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

            var branchError = await AssignSaleBranchAsync(tenantDb, transaction);
            if (branchError != null)
            {
                return branchError;
            }

            // Recording a sale is ONE request: the sale and all its items are saved together (nothing half-saved).
            // Prices come from the products on the server; the client's prices and totals are ignored.
            // An empty sale (items added later through Manage Items) is a management action only.
            var requestedItems = transaction.TransactionItems.ToList();
            transaction.TransactionId = 0;
            transaction.RowGuid = Guid.NewGuid();
            transaction.IsDeleted = false;
            transaction.TransactionItems = new List<TransactionItem>();

            if (requestedItems.Count == 0 && !HttpContext.HasTenantPermission(TenantPermissions.ManageSales))
            {
                return BadRequest(new { message = "Add at least one product to record the sale." });
            }

            // A sale with no items has no total yet; the client's TotalAmount is never used.
            transaction.TotalAmount = 0m;

            if (requestedItems.Count > 0)
            {
                decimal total = 0m;

                foreach (var requested in requestedItems)
                {
                    if (requested.Quantity <= 0 || requested.Quantity > InputRules.MaxQuantity)
                    {
                        return BadRequest(new { message = $"Quantity must be greater than zero and at most {InputRules.MaxQuantity:N0}." });
                    }

                    var product = await tenantDb.Products.FirstOrDefaultAsync(x => x.ProductId == requested.ProductId);

                    if (product == null || !string.Equals(product.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new { message = $"Product {requested.ProductId} does not exist or is inactive." });
                    }

                    // Same stock rules as Manage Items (branch stock on Branching plans, Product.Quantity otherwise).
                    var stockError = await BranchStock.TakeAsync(tenantDb, product, transaction.BranchId, requested.Quantity);
                    if (stockError != null)
                    {
                        return Conflict(new { code = "StockConflict", message = stockError });
                    }

                    var item = new TransactionItem
                    {
                        ProductId = product.ProductId,
                        Quantity = requested.Quantity,
                        UnitPrice = product.Price,
                        Subtotal = requested.Quantity * product.Price
                    };

                    transaction.TransactionItems.Add(item);
                    total += item.Subtotal;
                }

                transaction.TotalAmount = total;
            }

            bool usePromotions = HttpContext.HasTenantPermission(TenantPermissions.UsePromotions);
            bool useLoyalty = HttpContext.HasTenantPermission(TenantPermissions.UseLoyalty);

            if (!usePromotions && transaction.PromotionId.HasValue)
            {
                return FeatureNotIncluded("Promotions");
            }

            if (!useLoyalty && (transaction.PointsUsed != 0 || transaction.PointsEarned != 0))
            {
                return FeatureNotIncluded("Loyalty Points");
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

                string? promotionError = ValidatePromotion(promotion, transaction.TransactionDate, transaction.TotalAmount, transaction.BranchId);
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
            transaction.PointsEarned = useLoyalty ? CalculatePointsEarned(transaction.FinalAmount) : 0;

            transaction.Status = "Completed";

            tenantDb.SalesTransactions.Add(transaction);

            if (transaction.PointsEarned != 0 || transaction.PointsUsed != 0)
            {
                customer.LoyaltyPoints += transaction.PointsEarned - transaction.PointsUsed;

                tenantDb.LoyaltyTransactions.Add(new LoyaltyTransaction
                {
                    CustomerId = transaction.CustomerId,
                    SalesTransaction = transaction,
                    PointsEarned = transaction.PointsEarned,
                    PointsUsed = transaction.PointsUsed,
                    TransactionType = "Sale",
                    Date = transaction.TransactionDate
                });
            }

            // Sale, items, stock and loyalty points in one save.
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/sales/{transaction.TransactionId}",
                ToFlatResponse(transaction));
        }

        [HttpPut("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageSales)]
        public async Task<IActionResult> UpdateSalesTransaction(int companyId, int id, SalesTransaction updated)
        {
            // A sale is Completed or Cancelled; cancelling is the only status change (restores stock and points).
            string? requestedStatus = InputRules.OneOf(updated.Status, "Completed", "Cancelled");
            if (requestedStatus == null)
            {
                return BadRequest(InputRules.Message("Status must be Completed or Cancelled."));
            }

            updated.Status = requestedStatus;

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transaction = await tenantDb.SalesTransactions
                .FirstOrDefaultAsync(x => x.TransactionId == id);

            if (transaction == null)
            {
                return NotFound($"SalesTransaction with id {id} not found.");
            }

            if (!await BranchStock.CanChangeSaleAsync(HttpContext, tenantDb, transaction.BranchId))
            {
                return OtherBranch();
            }

            var customer = await tenantDb.Customers
                .FirstOrDefaultAsync(x => x.CustomerId == transaction.CustomerId);

            if (customer == null)
            {
                return BadRequest("Linked customer no longer exists.");
            }

            bool usePromotions = HttpContext.HasTenantPermission(TenantPermissions.UsePromotions);
            bool useLoyalty = HttpContext.HasTenantPermission(TenantPermissions.UseLoyalty);

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
                        await BranchStock.ReturnAsync(tenantDb, product, transaction.BranchId, item.Quantity);
                    }
                }

                if (useLoyalty)
                {
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
                }

                transaction.Status = "Cancelled";

                await tenantDb.SaveChangesAsync();

                return Ok(ToFlatResponse(transaction));
            }

            if (isAlreadyCancelled)
            {
                return Conflict("This transaction is already cancelled and cannot be modified further.");
            }

            string? fieldError = ValidateSaleFields(updated, transaction);
            if (fieldError != null)
            {
                return BadRequest(InputRules.Message(fieldError));
            }

            // The sale total is never taken from the client: editing a sale keeps the stored total
            // (it comes from the server-priced items). Discounts and points below are recalculated from it.
            updated.TotalAmount = transaction.TotalAmount;

            if (!usePromotions && updated.PromotionId != transaction.PromotionId)
            {
                return FeatureNotIncluded("Promotions");
            }

            if (!useLoyalty && (updated.PointsUsed != transaction.PointsUsed || updated.PointsEarned != transaction.PointsEarned))
            {
                return FeatureNotIncluded("Loyalty Points");
            }

            decimal discountAmount = 0;
            Promotion? appliedPromotion = null;

            if (updated.PromotionId.HasValue && !usePromotions)
            {
                appliedPromotion = await tenantDb.Promotions
                    .FirstOrDefaultAsync(x => x.PromotionId == updated.PromotionId.Value);

                discountAmount = Math.Min(transaction.DiscountAmount, updated.TotalAmount);
                updated.PointsUsed = transaction.PointsUsed;
            }
            else if (updated.PromotionId.HasValue)
            {
                var promotion = await tenantDb.Promotions
                    .FirstOrDefaultAsync(x => x.PromotionId == updated.PromotionId.Value);

                if (promotion == null)
                {
                    return BadRequest($"PromotionId {updated.PromotionId} does not exist for this tenant.");
                }

                string? promotionError = ValidatePromotion(promotion, updated.TransactionDate, updated.TotalAmount, transaction.BranchId);
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

            if (useLoyalty)
            {
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
            }
            else
            {
                updated.PointsEarned = transaction.PointsEarned;
                updated.PointsUsed = transaction.PointsUsed;
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
            transaction.Status = "Completed";

            await tenantDb.SaveChangesAsync();

            return Ok(ToFlatResponse(transaction));
        }

        [HttpDelete("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageSales)]
        public async Task<IActionResult> DeleteSalesTransaction(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transaction = await tenantDb.SalesTransactions
                .FirstOrDefaultAsync(x => x.TransactionId == id);

            if (transaction == null)
            {
                return NotFound($"SalesTransaction with id {id} not found.");
            }

            if (!await BranchStock.CanChangeSaleAsync(HttpContext, tenantDb, transaction.BranchId))
            {
                return OtherBranch();
            }

            if (string.Equals(transaction.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("A completed transaction cannot be deleted. Cancel it first so its stock and loyalty points are restored.");
            }

            transaction.IsDeleted = true;
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }

        // Branching plans: ADMIN uses the selected branch (or their assigned one); MANAGER / STAFF always use their
        // assigned branch. Other plans never store a branch, so their sales behave exactly as before.
        private async Task<IActionResult?> AssignSaleBranchAsync(
            freshcrumbs.CRM.infrastructure.data.TenantCrmDbContext tenantDb,
            SalesTransaction transaction)
        {
            if (!BranchStock.IsBranchingCompany(HttpContext))
            {
                transaction.BranchId = null;
                return null;
            }

            var assigned = await BranchStock.GetAssignedBranchAsync(tenantDb, User);

            if (!BranchStock.IsAdmin(HttpContext))
            {
                if (assigned == null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        code = "NoBranchAssigned",
                        message = "You are not assigned to a branch. Ask your administrator to assign you to a branch before recording sales."
                    });
                }

                transaction.BranchId = assigned.BranchId;
                return null;
            }

            transaction.BranchId ??= assigned?.BranchId;

            if (transaction.BranchId == null)
            {
                return BadRequest(new { message = "Select the branch for this sale." });
            }

            bool branchIsActive = await tenantDb.Branches
                .AnyAsync(b => b.BranchId == transaction.BranchId.Value && b.Status == "Active");

            if (!branchIsActive)
            {
                return BadRequest(new { message = "The selected branch does not exist or is inactive." });
            }

            return null;
        }

        private ObjectResult OtherBranch()
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "OtherBranch",
                message = "This sale belongs to another branch."
            });
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

        // Payment method from the fixed list (standard spelling; an older value already on the sale may stay) and a
        // real, non-future sale date.
        private static string? ValidateSaleFields(SalesTransaction sale, SalesTransaction? existing)
        {
            string? payment = InputRules.OneOf(sale.PaymentMethod, PaymentMethods)
                ?? (existing != null && sale.PaymentMethod == existing.PaymentMethod ? existing.PaymentMethod : null);

            if (payment == null)
            {
                return "Select a payment method: " + string.Join(", ", PaymentMethods) + ".";
            }

            sale.PaymentMethod = payment;

            if (!InputRules.IsRecordDate(sale.TransactionDate))
            {
                return "The sale date must be a valid date and cannot be in the future.";
            }

            return null;
        }

        private static readonly string[] PaymentMethods = { "Cash", "GCash", "Card", "Bank Transfer" };

        // saleBranchId is always the server-set branch of the sale. A branch promotion applies only at its branch; on a
        // plan without branches (sale branch = null) it never applies, so it cannot silently become company-wide.
        private static string? ValidatePromotion(Promotion promotion, DateTime transactionDate, decimal totalAmount, int? saleBranchId)
        {
            if (!string.Equals(promotion.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return $"Promotion \"{promotion.PromotionName}\" is not active.";
            }

            if (promotion.BranchId != null && promotion.BranchId != saleBranchId)
            {
                return $"Promotion \"{promotion.PromotionName}\" is only available at its own branch.";
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
                transaction.BranchId,
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