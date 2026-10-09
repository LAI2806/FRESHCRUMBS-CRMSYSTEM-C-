using System.Text.Json;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using freshcrumbs.CRM.infrastructure.services;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Services.Sync
{
    // CLOUD side. Applies the changes a desktop pushes:
    //   * idempotent  : every OperationId is recorded in SyncReceipts, a retry never duplicates anything
    //   * atomic      : all operations of one GroupId (e.g. a sale + its loyalty entry) commit together
    //   * conflicts   : last change wins per record (by the time the change was made), but stock and
    //                   loyalty points are added as deltas and a Cancelled sale never reopens
    //   * plan rules  : operations for features the company's plan does not include are rejected
    public class CloudSyncApplier
    {
        private readonly ITenantDbContextFactory _factory;

        public CloudSyncApplier(ITenantDbContextFactory factory)
        {
            _factory = factory;
        }

        public static string? FeatureFor(string entityType) => entityType switch
        {
            nameof(Customer) or nameof(CustomerDiscountEligibility) or nameof(Product)
                or nameof(SalesTransaction) or nameof(TransactionItem) => PlanFeatureKeys.MainTransactions,
            nameof(Feedback) or nameof(Inquiry) => PlanFeatureKeys.DataCollection,
            nameof(LoyaltyTransaction) or nameof(Promotion) => PlanFeatureKeys.ActionsRetention,
            nameof(Branch) or nameof(BranchInventory) or nameof(BranchAssignment) => PlanFeatureKeys.Branching,
            _ => null
        };

        public async Task<PushResponse> ApplyAsync(
            int companyId,
            PushRequest request,
            IReadOnlyCollection<string> features,
            CancellationToken ct)
        {
            var response = new PushResponse { ServerTimeUtc = DateTime.UtcNow };

            await using var db = await _factory.CreateAsync(companyId);

            // Imported values keep their original change time; nothing here is re-captured.
            db.ApplyingSync = true;
            db.CaptureSyncChanges = false;

            // Groups keep the order in which they were first seen (= order of creation on the desktop).
            var groups = request.Operations
                .GroupBy(o => o.GroupId)
                .Select(g => g.ToList())
                .ToList();

            var stopRemaining = false;

            foreach (var group in groups)
            {
                if (stopRemaining)
                {
                    // An earlier group failed: later groups may depend on it, so they wait for the next attempt.
                    response.Results.AddRange(group.Select(o => new PushOperationResult
                    {
                        OperationId = o.OperationId,
                        Result = PushResult.Failed,
                        Detail = "Waiting for an earlier change to be accepted."
                    }));
                    continue;
                }

                var results = await ApplyGroupAsync(db, group, features, ct);
                response.Results.AddRange(results);

                if (results.Any(r => r.Result == PushResult.Failed))
                {
                    stopRemaining = true;
                }
            }

            return response;
        }

        private static async Task<List<PushOperationResult>> ApplyGroupAsync(
            TenantCrmDbContext db,
            List<PushOperationDto> group,
            IReadOnlyCollection<string> features,
            CancellationToken ct)
        {
            var results = new List<PushOperationResult>();

            // Permanent rejection (plan does not include the feature): reject the whole group, apply nothing.
            foreach (var op in group)
            {
                var feature = FeatureFor(op.EntityType);

                if (feature != null && !features.Contains(feature, StringComparer.OrdinalIgnoreCase))
                {
                    var detail = $"The current plan does not include {PlanFeatureKeys.DisplayName(feature)}.";

                    db.ChangeTracker.Clear();
                    foreach (var o in group)
                    {
                        db.SyncReceipts.Add(new SyncReceipt
                        {
                            OperationId = o.OperationId,
                            EntityType = o.EntityType,
                            EntityRowGuid = o.EntityRowGuid,
                            Result = PushResult.Rejected,
                            Detail = detail
                        });
                    }

                    try
                    {
                        await db.SaveChangesAsync(ct);
                    }
                    catch (DbUpdateException)
                    {
                        // Receipts already stored by an earlier attempt: the rejection is still the answer.
                        db.ChangeTracker.Clear();
                    }

                    return group.Select(o => new PushOperationResult
                    {
                        OperationId = o.OperationId,
                        Result = PushResult.Rejected,
                        Detail = detail
                    }).ToList();
                }
            }

            try
            {
                var strategy = db.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();
                    results.Clear();

                    await using var tx = await db.Database.BeginTransactionAsync(ct);

                    foreach (var op in group)
                    {
                        results.Add(await ApplyOneAsync(db, op, ct));
                    }

                    await tx.CommitAsync(ct);
                });

                return results;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();

                // The transaction rolled back: nothing of this group was applied, so the desktop retries it whole.
                return group.Select(o => new PushOperationResult
                {
                    OperationId = o.OperationId,
                    Result = PushResult.Failed,
                    Detail = Truncate(ex.GetBaseException().Message)
                }).ToList();
            }
        }

        private static async Task<PushOperationResult> ApplyOneAsync(TenantCrmDbContext db, PushOperationDto op, CancellationToken ct)
        {
            // Duplicate delivery (the desktop retried after an interrupted connection)?
            var previous = await db.SyncReceipts.AsNoTracking().FirstOrDefaultAsync(r => r.OperationId == op.OperationId, ct);

            if (previous != null)
            {
                return new PushOperationResult { OperationId = op.OperationId, Result = PushResult.Duplicate, Detail = previous.Detail };
            }

            string result;
            string? detail = null;

            if (op.Operation == SyncOperationType.Delete)
            {
                result = await DeleteAsync(db, op, ct);
            }
            else
            {
                (result, detail) = await UpsertAsync(db, op, absolute: false, ct);
            }

            db.SyncReceipts.Add(new SyncReceipt
            {
                OperationId = op.OperationId,
                EntityType = op.EntityType,
                EntityRowGuid = op.EntityRowGuid,
                Result = result,
                Detail = detail
            });

            await db.SaveChangesAsync(ct);

            return new PushOperationResult { OperationId = op.OperationId, Result = result, Detail = detail };
        }

        // ------------------------------------------------------------------ upsert

        // absolute = false: a push from a desktop (stock/points are added as deltas, duplicate codes are renumbered).
        // absolute = true : a desktop applying data pulled from the cloud (cloud values replace local ones).
        internal static async Task<(string Result, string? Detail)> UpsertAsync(TenantCrmDbContext db, PushOperationDto op, bool absolute, CancellationToken ct)
        {
            var p = op.Payload;
            var g = op.EntityRowGuid;
            var at = DateTime.SpecifyKind(op.ChangedAtUtc, DateTimeKind.Utc);

            switch (op.EntityType)
            {
                case nameof(Customer):
                    {
                        var e = await db.Customers.FirstOrDefaultAsync(x => x.RowGuid == g, ct);
                        string? detail = null;

                        if (e == null)
                        {
                            e = new Customer { RowGuid = g, CreatedAt = Dt(p, "CreatedAt") ?? at };
                            db.Customers.Add(e);

                            var code = Str(p, "CustomerCode");

                            if (await db.Customers.AnyAsync(x => x.CustomerCode == code, ct))
                            {
                                if (absolute)
                                {
                                    db.Entry(e).State = EntityState.Detached;
                                    return (PushResult.Failed, "Customer code already used by another local record.");
                                }

                                // Two devices created the same code while offline: the cloud assigns the next free one.
                                code = await NextCodeAsync(db.Customers.Select(x => x.CustomerCode), "CUST-", ct);
                                detail = $"Customer code changed to {code} (duplicate code created offline).";
                            }

                            var registered = await ReadBranchAsync(db, p, ct);

                            if (registered.NotInCloudYet)
                            {
                                db.Entry(e).State = EntityState.Detached;
                                return (PushResult.Failed, "The branch for this customer is not in the cloud yet.");
                            }

                            e.CustomerCode = code;
                            e.LoyaltyPoints = Int(p, "LoyaltyPoints");
                            e.BranchId = registered.BranchId;
                            CopyCustomer(e, p);
                            e.UpdatedAt = at;
                            return (PushResult.Applied, detail);
                        }

                        // The registration branch is set once; an older record without one may receive it.
                        if (e.BranchId == null)
                        {
                            var registeredLater = await ReadBranchAsync(db, p, ct);
                            if (registeredLater.HasKey && !registeredLater.NotInCloudYet) e.BranchId = registeredLater.BranchId;
                        }

                        var applied = at >= e.UpdatedAt;

                        if (applied)
                        {
                            CopyCustomer(e, p);
                            e.UpdatedAt = at;
                        }

                        if (absolute)
                        {
                            if (applied) e.LoyaltyPoints = Int(p, "LoyaltyPoints");
                        }
                        else
                        {
                            // Loyalty points are added, never overwritten.
                            var delta = NullableInt(p, "LoyaltyPointsDelta");
                            if (delta != null) e.LoyaltyPoints += delta.Value;
                        }

                        return applied ? (PushResult.Applied, null) : (PushResult.Skipped, "A newer cloud change to this customer was kept.");
                    }

                case nameof(Product):
                    {
                        var e = await db.Products.FirstOrDefaultAsync(x => x.RowGuid == g, ct);
                        string? detail = null;

                        if (e == null)
                        {
                            e = new Product { RowGuid = g, CreatedAt = Dt(p, "CreatedAt") ?? at };
                            db.Products.Add(e);

                            var code = Str(p, "ProductCode");

                            if (await db.Products.AnyAsync(x => x.ProductCode == code, ct))
                            {
                                if (absolute)
                                {
                                    db.Entry(e).State = EntityState.Detached;
                                    return (PushResult.Failed, "Product code already used by another local record.");
                                }

                                code = await NextCodeAsync(db.Products.Select(x => x.ProductCode), "PROD-", ct);
                                detail = $"Product code changed to {code} (duplicate code created offline).";
                            }

                            e.ProductCode = code;
                            e.Quantity = Int(p, "Quantity");
                            CopyProduct(e, p);
                            e.UpdatedAt = at;
                            return (PushResult.Applied, detail);
                        }

                        var applied = at >= e.UpdatedAt;

                        if (applied)
                        {
                            CopyProduct(e, p);
                            e.UpdatedAt = at;
                        }

                        if (absolute)
                        {
                            if (applied) e.Quantity = Int(p, "Quantity");
                        }
                        else
                        {
                            // Stock is adjusted by the quantity the device added/removed, so sales from several devices add up.
                            var delta = NullableInt(p, "QuantityDelta");
                            if (delta != null) e.Quantity += delta.Value;
                        }

                        return applied ? (PushResult.Applied, null) : (PushResult.Skipped, "A newer cloud change to this product was kept.");
                    }

                case nameof(Promotion):
                    {
                        var promotionBranch = await ReadBranchAsync(db, p, ct);
                        if (promotionBranch.NotInCloudYet) return (PushResult.Failed, "The branch for this promotion is not in the cloud yet.");

                        var e = await db.Promotions.FirstOrDefaultAsync(x => x.RowGuid == g, ct);

                        if (e == null)
                        {
                            e = new Promotion { RowGuid = g };
                            db.Promotions.Add(e);
                        }
                        else if (at < e.UpdatedAt)
                        {
                            return (PushResult.Skipped, "A newer cloud change to this promotion was kept.");
                        }

                        e.PromotionName = Str(p, "PromotionName");
                        e.Description = Str(p, "Description");
                        e.DiscountType = Str(p, "DiscountType");
                        e.DiscountValue = Dec(p, "DiscountValue");
                        e.MinimumPurchase = Dec(p, "MinimumPurchase");
                        e.RequiredLoyaltyPoints = Int(p, "RequiredLoyaltyPoints");
                        e.StartDate = Dt(p, "StartDate") ?? e.StartDate;
                        e.EndDate = Dt(p, "EndDate") ?? e.EndDate;
                        e.Status = Str(p, "Status");
                        e.EligibilityCategory = NullableStr(p, "EligibilityCategory");
                        if (promotionBranch.HasKey) e.BranchId = promotionBranch.BranchId;
                        e.UpdatedAt = at;
                        return (PushResult.Applied, null);
                    }

                case nameof(CustomerDiscountEligibility):
                    {
                        var customerId = await IdOfAsync(db, nameof(Customer), GuidOrNull(p, "CustomerRowGuid"), ct);
                        if (customerId == null) return (PushResult.Failed, "The customer for this record is not in the cloud yet.");

                        var e = await db.CustomerDiscountEligibilities.FirstOrDefaultAsync(x => x.RowGuid == g, ct);

                        if (e == null)
                        {
                            // One eligibility per customer and category (unique index): merge instead of failing.
                            var category = Str(p, "Category");
                            e = await db.CustomerDiscountEligibilities
                                .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.Category == category, ct);

                            if (e == null)
                            {
                                e = new CustomerDiscountEligibility { RowGuid = g };
                                db.CustomerDiscountEligibilities.Add(e);
                            }
                        }
                        else if (at < e.UpdatedAt)
                        {
                            return (PushResult.Skipped, "A newer cloud change was kept.");
                        }

                        e.CustomerId = customerId.Value;
                        e.Category = Str(p, "Category");
                        e.IdNumber = Str(p, "IdNumber");
                        e.VerificationStatus = Str(p, "VerificationStatus");
                        e.UpdatedAt = at;
                        return (PushResult.Applied, null);
                    }

                case nameof(SalesTransaction):
                    {
                        var customerId = await IdOfAsync(db, nameof(Customer), GuidOrNull(p, "CustomerRowGuid"), ct);
                        if (customerId == null) return (PushResult.Failed, "The customer for this sale is not in the cloud yet.");

                        int? promotionId = null;
                        var promotionGuid = GuidOrNull(p, "PromotionRowGuid");
                        if (promotionGuid != null)
                        {
                            promotionId = await IdOfAsync(db, nameof(Promotion), promotionGuid, ct);
                            if (promotionId == null) return (PushResult.Failed, "The promotion for this sale is not in the cloud yet.");
                        }

                        int? branchId = null;
                        var branchGuid = GuidOrNull(p, "BranchRowGuid");
                        if (branchGuid != null)
                        {
                            branchId = await IdOfAsync(db, nameof(Branch), branchGuid, ct);
                            if (branchId == null) return (PushResult.Failed, "The branch for this sale is not in the cloud yet.");
                        }

                        var e = await db.SalesTransactions.FirstOrDefaultAsync(x => x.RowGuid == g, ct);

                        if (e == null)
                        {
                            e = new SalesTransaction { RowGuid = g };
                            db.SalesTransactions.Add(e);
                        }
                        else
                        {
                            // A cancelled sale is final: it can never be reopened by an older edit from another device.
                            var cloudCancelled = string.Equals(e.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

                            if (at < e.UpdatedAt && !(Str(p, "Status").Equals("Cancelled", StringComparison.OrdinalIgnoreCase) && !cloudCancelled))
                            {
                                return (PushResult.Skipped, "A newer cloud change to this sale was kept.");
                            }

                            if (cloudCancelled)
                            {
                                return (PushResult.Skipped, "This sale is already cancelled in the cloud.");
                            }
                        }

                        e.CustomerId = customerId.Value;
                        e.PromotionId = promotionId;

                        // Payloads from desktops without branch support carry no BranchRowGuid: keep the stored branch.
                        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("BranchRowGuid", out _))
                        {
                            e.BranchId = branchId;
                        }

                        e.TransactionDate = Dt(p, "TransactionDate") ?? e.TransactionDate;
                        e.TotalAmount = Dec(p, "TotalAmount");
                        e.DiscountAmount = Dec(p, "DiscountAmount");
                        e.CustomerDiscountAmount = Dec(p, "CustomerDiscountAmount");
                        e.PointsUsed = Int(p, "PointsUsed");
                        e.PointsEarned = Int(p, "PointsEarned");
                        e.FinalAmount = Dec(p, "FinalAmount");
                        e.PaymentMethod = Str(p, "PaymentMethod");
                        e.Status = Str(p, "Status");
                        e.IsDeleted = Bool(p, "IsDeleted");
                        e.UpdatedAt = at;
                        return (PushResult.Applied, null);
                    }

                case nameof(TransactionItem):
                    {
                        var saleId = await IdOfAsync(db, nameof(SalesTransaction), GuidOrNull(p, "SalesTransactionRowGuid"), ct);
                        var productId = await IdOfAsync(db, nameof(Product), GuidOrNull(p, "ProductRowGuid"), ct);
                        if (saleId == null || productId == null) return (PushResult.Failed, "The sale or product for this item is not in the cloud yet.");

                        var e = await db.TransactionItems.FirstOrDefaultAsync(x => x.RowGuid == g, ct);

                        if (e == null)
                        {
                            e = new TransactionItem { RowGuid = g };
                            db.TransactionItems.Add(e);
                        }
                        else if (at < e.UpdatedAt)
                        {
                            return (PushResult.Skipped, "A newer cloud change was kept.");
                        }

                        // NOTE: stock is NOT touched here; the stock change arrives as the Product QuantityDelta.
                        e.TransactionId = saleId.Value;
                        e.ProductId = productId.Value;
                        e.Quantity = Int(p, "Quantity");
                        e.UnitPrice = Dec(p, "UnitPrice");
                        e.Subtotal = Dec(p, "Subtotal");
                        e.UpdatedAt = at;
                        return (PushResult.Applied, null);
                    }

                case nameof(LoyaltyTransaction):
                    {
                        var customerId = await IdOfAsync(db, nameof(Customer), GuidOrNull(p, "CustomerRowGuid"), ct);
                        if (customerId == null) return (PushResult.Failed, "The customer for this loyalty entry is not in the cloud yet.");

                        int? saleId = null;
                        var saleGuid = GuidOrNull(p, "SalesTransactionRowGuid");
                        if (saleGuid != null)
                        {
                            saleId = await IdOfAsync(db, nameof(SalesTransaction), saleGuid, ct);
                            if (saleId == null) return (PushResult.Failed, "The sale for this loyalty entry is not in the cloud yet.");
                        }

                        var e = await db.LoyaltyTransactions.FirstOrDefaultAsync(x => x.RowGuid == g, ct);

                        if (e == null)
                        {
                            e = new LoyaltyTransaction { RowGuid = g };
                            db.LoyaltyTransactions.Add(e);
                        }
                        else if (at < e.UpdatedAt)
                        {
                            return (PushResult.Skipped, "A newer cloud change was kept.");
                        }

                        // NOTE: the customer's points total arrives as the Customer LoyaltyPointsDelta.
                        e.CustomerId = customerId.Value;
                        e.SalesTransactionId = saleId;
                        e.PointsEarned = Int(p, "PointsEarned");
                        e.PointsUsed = Int(p, "PointsUsed");
                        e.TransactionType = Str(p, "TransactionType");
                        e.Date = Dt(p, "Date") ?? e.Date;
                        e.IsDeleted = Bool(p, "IsDeleted");
                        e.UpdatedAt = at;
                        return (PushResult.Applied, null);
                    }

                case nameof(Feedback):
                    {
                        var customerId = await IdOfAsync(db, nameof(Customer), GuidOrNull(p, "CustomerRowGuid"), ct);
                        if (customerId == null) return (PushResult.Failed, "The customer for this feedback is not in the cloud yet.");

                        var feedbackBranch = await ReadBranchAsync(db, p, ct);
                        if (feedbackBranch.NotInCloudYet) return (PushResult.Failed, "The branch for this feedback is not in the cloud yet.");

                        var e = await db.Feedbacks.FirstOrDefaultAsync(x => x.RowGuid == g, ct);

                        if (e == null)
                        {
                            e = new Feedback { RowGuid = g };
                            db.Feedbacks.Add(e);
                        }
                        else if (at < e.UpdatedAt)
                        {
                            return (PushResult.Skipped, "A newer cloud change was kept.");
                        }

                        e.CustomerId = customerId.Value;
                        e.Type = Str(p, "Type");
                        e.Category = Str(p, "Category");
                        e.Comment = Str(p, "Comment");
                        e.DateSubmitted = Dt(p, "DateSubmitted") ?? e.DateSubmitted;
                        e.Status = Str(p, "Status");
                        e.IsDeleted = Bool(p, "IsDeleted");
                        if (feedbackBranch.HasKey) e.BranchId = feedbackBranch.BranchId;
                        e.UpdatedAt = at;
                        return (PushResult.Applied, null);
                    }

                case nameof(Inquiry):
                    {
                        var customerId = await IdOfAsync(db, nameof(Customer), GuidOrNull(p, "CustomerRowGuid"), ct);
                        if (customerId == null) return (PushResult.Failed, "The customer for this inquiry is not in the cloud yet.");

                        var inquiryBranch = await ReadBranchAsync(db, p, ct);
                        if (inquiryBranch.NotInCloudYet) return (PushResult.Failed, "The branch for this inquiry is not in the cloud yet.");

                        var e = await db.Inquiries.FirstOrDefaultAsync(x => x.RowGuid == g, ct);

                        if (e == null)
                        {
                            e = new Inquiry { RowGuid = g };
                            db.Inquiries.Add(e);
                        }
                        else if (at < e.UpdatedAt)
                        {
                            return (PushResult.Skipped, "A newer cloud change was kept.");
                        }

                        e.CustomerId = customerId.Value;
                        e.Type = Str(p, "Type");
                        e.Source = Str(p, "Source");
                        e.Subject = Str(p, "Subject");
                        e.Message = Str(p, "Message");
                        e.DateSubmitted = Dt(p, "DateSubmitted") ?? e.DateSubmitted;
                        e.Status = Str(p, "Status");
                        e.Response = Str(p, "Response");
                        e.RespondedBy = Str(p, "RespondedBy");
                        e.RespondedAt = Dt(p, "RespondedAt");
                        e.IsDeleted = Bool(p, "IsDeleted");
                        if (inquiryBranch.HasKey) e.BranchId = inquiryBranch.BranchId;
                        e.UpdatedAt = at;
                        return (PushResult.Applied, null);
                    }

                case nameof(Branch):
                    {
                        var e = await db.Branches.FirstOrDefaultAsync(x => x.RowGuid == g, ct);

                        if (e == null)
                        {
                            e = new Branch { RowGuid = g, CreatedAt = Dt(p, "CreatedAt") ?? at };
                            db.Branches.Add(e);
                        }
                        else if (at < e.UpdatedAt)
                        {
                            return (PushResult.Skipped, "A newer cloud change to this branch was kept.");
                        }

                        e.BranchName = Str(p, "BranchName");
                        e.Address = Str(p, "Address");
                        e.Status = Str(p, "Status");
                        e.UpdatedAt = at;
                        return (PushResult.Applied, null);
                    }

                case nameof(BranchInventory):
                    {
                        var branchId = await IdOfAsync(db, nameof(Branch), GuidOrNull(p, "BranchRowGuid"), ct);
                        var productId = await IdOfAsync(db, nameof(Product), GuidOrNull(p, "ProductRowGuid"), ct);
                        if (branchId == null || productId == null) return (PushResult.Failed, "The branch or product for this stock record is not in the cloud yet.");

                        var e = await db.BranchInventories.FirstOrDefaultAsync(x => x.RowGuid == g, ct);

                        if (e == null)
                        {
                            // One row per branch and product (unique index). The same pair may have been created on
                            // another device with a different RowGuid: merge into it instead of failing.
                            e = await db.BranchInventories
                                .FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId, ct);

                            if (e == null)
                            {
                                db.BranchInventories.Add(new BranchInventory
                                {
                                    RowGuid = g,
                                    BranchId = branchId.Value,
                                    ProductId = productId.Value,
                                    Quantity = Int(p, "Quantity"),
                                    UpdatedAt = at
                                });
                                return (PushResult.Applied, null);
                            }

                            if (absolute)
                            {
                                e.Quantity = Int(p, "Quantity");
                            }
                            else
                            {
                                e.Quantity += NullableInt(p, "QuantityDelta") ?? Int(p, "Quantity");
                            }

                            if (at > e.UpdatedAt) e.UpdatedAt = at;
                            return (PushResult.Applied, null);
                        }

                        var applied = at >= e.UpdatedAt;

                        if (absolute)
                        {
                            if (applied) e.Quantity = Int(p, "Quantity");
                        }
                        else
                        {
                            // Branch stock is adjusted by what the device added/removed, so several devices add up.
                            var delta = NullableInt(p, "QuantityDelta");
                            if (delta != null) e.Quantity += delta.Value;
                        }

                        if (applied) e.UpdatedAt = at;
                        return (PushResult.Applied, null);
                    }

                case nameof(BranchAssignment):
                    {
                        int? branchId = null;
                        var branchGuid = GuidOrNull(p, "BranchRowGuid");
                        if (branchGuid != null)
                        {
                            branchId = await IdOfAsync(db, nameof(Branch), branchGuid, ct);
                            if (branchId == null) return (PushResult.Failed, "The branch for this assignment is not in the cloud yet.");
                        }

                        var userId = Str(p, "UserId");
                        var e = await db.BranchAssignments.FirstOrDefaultAsync(x => x.RowGuid == g, ct)
                            ?? await db.BranchAssignments.FirstOrDefaultAsync(x => x.UserId == userId, ct);

                        if (e == null)
                        {
                            e = new BranchAssignment { RowGuid = g, UserId = userId };
                            db.BranchAssignments.Add(e);
                        }
                        else if (at < e.UpdatedAt)
                        {
                            return (PushResult.Skipped, "A newer cloud change to this assignment was kept.");
                        }

                        e.UserName = Str(p, "UserName");
                        e.BranchId = branchId;
                        e.UpdatedAt = at;
                        return (PushResult.Applied, null);
                    }

                default:
                    return (PushResult.Rejected, $"Unknown entity type '{op.EntityType}'.");
            }
        }

        private static void CopyCustomer(Customer e, JsonElement p)
        {
            e.FirstName = Str(p, "FirstName");
            e.LastName = Str(p, "LastName");
            e.Email = Str(p, "Email");
            e.ContactNo = Str(p, "ContactNo");
            e.Address = Str(p, "Address");
            e.Status = Str(p, "Status");
        }

        private static void CopyProduct(Product e, JsonElement p)
        {
            e.ProductName = Str(p, "ProductName");
            e.Category = Str(p, "Category");
            e.Description = Str(p, "Description");
            e.Price = Dec(p, "Price");
            e.ReorderLevel = Int(p, "ReorderLevel");
            e.Status = Str(p, "Status");
        }

        // Branch association sent as BranchRowGuid. HasKey = false for payloads from desktops without branch support
        // (keep the stored value); NotInCloudYet = the branch has not been synchronized yet (retry later).
        private static async Task<(bool HasKey, int? BranchId, bool NotInCloudYet)> ReadBranchAsync(
            TenantCrmDbContext db, JsonElement p, CancellationToken ct)
        {
            if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty("BranchRowGuid", out _))
            {
                return (false, null, false);
            }

            var branchGuid = GuidOrNull(p, "BranchRowGuid");

            if (branchGuid == null)
            {
                return (true, null, false);
            }

            var branchId = await IdOfAsync(db, nameof(Branch), branchGuid, ct);
            return (true, branchId, branchId == null);
        }

        // ------------------------------------------------------------------ delete

        private static async Task<string> DeleteAsync(TenantCrmDbContext db, PushOperationDto op, CancellationToken ct)
        {
            var g = op.EntityRowGuid;

            switch (op.EntityType)
            {
                case nameof(Customer):
                    return await RemoveAsync(db, await db.Customers.FirstOrDefaultAsync(x => x.RowGuid == g, ct));
                case nameof(Product):
                    return await RemoveAsync(db, await db.Products.FirstOrDefaultAsync(x => x.RowGuid == g, ct));
                case nameof(Promotion):
                    return await RemoveAsync(db, await db.Promotions.FirstOrDefaultAsync(x => x.RowGuid == g, ct));
                case nameof(CustomerDiscountEligibility):
                    return await RemoveAsync(db, await db.CustomerDiscountEligibilities.FirstOrDefaultAsync(x => x.RowGuid == g, ct));
                case nameof(SalesTransaction):
                    return await RemoveAsync(db, await db.SalesTransactions.FirstOrDefaultAsync(x => x.RowGuid == g, ct));
                case nameof(TransactionItem):
                    return await RemoveAsync(db, await db.TransactionItems.FirstOrDefaultAsync(x => x.RowGuid == g, ct));
                case nameof(LoyaltyTransaction):
                    return await RemoveAsync(db, await db.LoyaltyTransactions.FirstOrDefaultAsync(x => x.RowGuid == g, ct));
                case nameof(Feedback):
                    return await RemoveAsync(db, await db.Feedbacks.FirstOrDefaultAsync(x => x.RowGuid == g, ct));
                case nameof(Inquiry):
                    return await RemoveAsync(db, await db.Inquiries.FirstOrDefaultAsync(x => x.RowGuid == g, ct));
                default:
                    return PushResult.Rejected;
            }
        }

        private static Task<string> RemoveAsync(TenantCrmDbContext db, object? entity)
        {
            // Already gone = deleted: the result is the same, so a repeated delete is harmless.
            if (entity != null)
            {
                db.Remove(entity);
            }

            return Task.FromResult(PushResult.Applied);
        }

        // ------------------------------------------------------------------ helpers

        private static async Task<int?> IdOfAsync(TenantCrmDbContext db, string type, Guid? rowGuid, CancellationToken ct)
        {
            if (rowGuid == null)
            {
                return null;
            }

            var g = rowGuid.Value;

            return type switch
            {
                nameof(Customer) => await db.Customers.AsNoTracking().Where(x => x.RowGuid == g).Select(x => (int?)x.CustomerId).FirstOrDefaultAsync(ct),
                nameof(Product) => await db.Products.AsNoTracking().Where(x => x.RowGuid == g).Select(x => (int?)x.ProductId).FirstOrDefaultAsync(ct),
                nameof(Promotion) => await db.Promotions.AsNoTracking().Where(x => x.RowGuid == g).Select(x => (int?)x.PromotionId).FirstOrDefaultAsync(ct),
                nameof(SalesTransaction) => await db.SalesTransactions.AsNoTracking().Where(x => x.RowGuid == g).Select(x => (int?)x.TransactionId).FirstOrDefaultAsync(ct),
                nameof(Branch) => await db.Branches.AsNoTracking().Where(x => x.RowGuid == g).Select(x => (int?)x.BranchId).FirstOrDefaultAsync(ct),
                _ => null
            };
        }

        // Next free code like CUST-004 / PROD-012, same format the desktop UI generates.
        // Also used by the customers "next-code" endpoint so new codes are counted across the whole company.
        internal static async Task<string> NextCodeAsync(IQueryable<string> codes, string prefix, CancellationToken ct)
        {
            var existing = await codes.Where(c => c.StartsWith(prefix)).ToListAsync(ct);
            var max = 0;

            foreach (var code in existing)
            {
                if (int.TryParse(code.Substring(prefix.Length), out var n) && n > max)
                {
                    max = n;
                }
            }

            return $"{prefix}{max + 1:D3}";
        }

        private static string Truncate(string s) => s.Length <= 450 ? s : s.Substring(0, 450);

        private static string Str(JsonElement p, string name) =>
            p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? string.Empty
                : string.Empty;

        private static string? NullableStr(JsonElement p, string name) =>
            p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static int Int(JsonElement p, string name) =>
            p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32()
                : 0;

        private static int? NullableInt(JsonElement p, string name) =>
            p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32()
                : null;

        private static decimal Dec(JsonElement p, string name) =>
            p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDecimal()
                : 0m;

        private static bool Bool(JsonElement p, string name) =>
            p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v)
                && (v.ValueKind == JsonValueKind.True);

        private static DateTime? Dt(JsonElement p, string name) =>
            p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                && DateTime.TryParse(v.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var d)
                ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
                : null;

        private static Guid? GuidOrNull(JsonElement p, string name) =>
            p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                && Guid.TryParse(v.GetString(), out var g)
                ? g
                : null;
    }
}