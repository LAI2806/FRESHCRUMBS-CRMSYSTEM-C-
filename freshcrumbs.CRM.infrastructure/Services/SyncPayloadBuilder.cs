using System.Text.Json;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.infrastructure.Services
{
    // A change detected by TenantCrmDbContext.SaveChangesAsync before the data was saved.
    internal sealed class CapturedSyncChange
    {
        public ISyncEntity Entity { get; set; } = null!;

        public EntityState State { get; set; }

        public Guid RowGuid { get; set; }

        // Quantity delta (Product, BranchInventory) or loyalty-points delta (Customer) for modified rows.
        public int? Delta { get; set; }
    }

    // Turns captured changes into SyncOutbox rows. Property names in the payload match the entity
    // property names; parents are referenced by RowGuid ("<Parent>RowGuid") because integer
    // keys are only meaningful inside one database.
    public static class SyncPayloadBuilder
    {
        internal static async Task<List<SyncOutboxEntry>> BuildOutboxRowsAsync(
            TenantCrmDbContext db,
            List<CapturedSyncChange> changes,
            Guid groupId,
            CancellationToken ct)
        {
            var rows = new List<SyncOutboxEntry>();
            var cache = new Dictionary<string, Guid?>();

            foreach (var change in changes)
            {
                var entity = change.Entity;
                var typeName = entity.GetType().Name;

                var row = new SyncOutboxEntry
                {
                    OperationId = Guid.NewGuid(),
                    GroupId = groupId,
                    EntityType = typeName,
                    EntityRowGuid = change.RowGuid,
                    ChangedAtUtc = change.State == EntityState.Deleted ? DateTime.UtcNow : entity.UpdatedAt,
                    Status = SyncOutboxStatus.Pending
                };

                if (change.State == EntityState.Deleted)
                {
                    row.Operation = SyncOperationType.Delete;
                    row.Payload = "{}";
                }
                else
                {
                    row.Operation = SyncOperationType.Upsert;
                    var payload = await BuildPayloadAsync(db, entity, change, cache, ct);
                    row.Payload = JsonSerializer.Serialize(payload);
                }

                rows.Add(row);
            }

            return rows;
        }

        // Same payload shape as the outbox, used by the cloud to answer a pull request.
        public static Task<Dictionary<string, object?>> BuildPullPayloadAsync(
            TenantCrmDbContext db,
            ISyncEntity entity,
            Dictionary<string, Guid?> parentCache,
            CancellationToken ct)
        {
            return BuildPayloadAsync(
                db,
                entity,
                new CapturedSyncChange { Entity = entity, RowGuid = entity.RowGuid },
                parentCache,
                ct);
        }

        private static async Task<Dictionary<string, object?>> BuildPayloadAsync(
            TenantCrmDbContext db,
            ISyncEntity entity,
            CapturedSyncChange change,
            Dictionary<string, Guid?> cache,
            CancellationToken ct)
        {
            var p = new Dictionary<string, object?>
            {
                ["UpdatedAt"] = entity.UpdatedAt
            };

            switch (entity)
            {
                case Customer c:
                    p["CustomerCode"] = c.CustomerCode;
                    p["FirstName"] = c.FirstName;
                    p["LastName"] = c.LastName;
                    p["Email"] = c.Email;
                    p["ContactNo"] = c.ContactNo;
                    p["Address"] = c.Address;
                    p["LoyaltyPoints"] = c.LoyaltyPoints;
                    p["LoyaltyPointsDelta"] = change.Delta;
                    p["Status"] = c.Status;
                    p["CreatedAt"] = c.CreatedAt;
                    p["BranchRowGuid"] = c.BranchId == null
                        ? null
                        : await GuidOfAsync(db, "Branch", c.BranchId.Value, cache, ct);
                    break;

                case Product pr:
                    p["ProductCode"] = pr.ProductCode;
                    p["ProductName"] = pr.ProductName;
                    p["Category"] = pr.Category;
                    p["Description"] = pr.Description;
                    p["Price"] = pr.Price;
                    p["Quantity"] = pr.Quantity;
                    p["QuantityDelta"] = change.Delta;
                    p["ReorderLevel"] = pr.ReorderLevel;
                    p["Status"] = pr.Status;
                    p["CreatedAt"] = pr.CreatedAt;
                    break;

                case Promotion pm:
                    p["PromotionName"] = pm.PromotionName;
                    p["Description"] = pm.Description;
                    p["DiscountType"] = pm.DiscountType;
                    p["DiscountValue"] = pm.DiscountValue;
                    p["MinimumPurchase"] = pm.MinimumPurchase;
                    p["RequiredLoyaltyPoints"] = pm.RequiredLoyaltyPoints;
                    p["StartDate"] = pm.StartDate;
                    p["EndDate"] = pm.EndDate;
                    p["Status"] = pm.Status;
                    p["EligibilityCategory"] = pm.EligibilityCategory;
                    p["BranchRowGuid"] = pm.BranchId == null ? null : await GuidOfAsync(db, "Branch", pm.BranchId.Value, cache, ct);
                    break;

                case CustomerDiscountEligibility e:
                    p["CustomerRowGuid"] = await GuidOfAsync(db, "Customer", e.CustomerId, cache, ct);
                    p["Category"] = e.Category;
                    p["IdNumber"] = e.IdNumber;
                    p["VerificationStatus"] = e.VerificationStatus;
                    break;

                case SalesTransaction s:
                    p["CustomerRowGuid"] = await GuidOfAsync(db, "Customer", s.CustomerId, cache, ct);
                    p["PromotionRowGuid"] = s.PromotionId == null
                        ? null
                        : await GuidOfAsync(db, "Promotion", s.PromotionId.Value, cache, ct);
                    p["BranchRowGuid"] = s.BranchId == null
                        ? null
                        : await GuidOfAsync(db, "Branch", s.BranchId.Value, cache, ct);
                    p["TransactionDate"] = s.TransactionDate;
                    p["TotalAmount"] = s.TotalAmount;
                    p["DiscountAmount"] = s.DiscountAmount;
                    p["CustomerDiscountAmount"] = s.CustomerDiscountAmount;
                    p["PointsUsed"] = s.PointsUsed;
                    p["PointsEarned"] = s.PointsEarned;
                    p["FinalAmount"] = s.FinalAmount;
                    p["PaymentMethod"] = s.PaymentMethod;
                    p["Status"] = s.Status;
                    p["IsDeleted"] = s.IsDeleted;
                    break;

                case TransactionItem t:
                    p["SalesTransactionRowGuid"] = await GuidOfAsync(db, "SalesTransaction", t.TransactionId, cache, ct);
                    p["ProductRowGuid"] = await GuidOfAsync(db, "Product", t.ProductId, cache, ct);
                    p["Quantity"] = t.Quantity;
                    p["UnitPrice"] = t.UnitPrice;
                    p["Subtotal"] = t.Subtotal;
                    break;

                case LoyaltyTransaction l:
                    p["CustomerRowGuid"] = await GuidOfAsync(db, "Customer", l.CustomerId, cache, ct);
                    p["SalesTransactionRowGuid"] = l.SalesTransactionId == null
                        ? null
                        : await GuidOfAsync(db, "SalesTransaction", l.SalesTransactionId.Value, cache, ct);
                    p["PointsEarned"] = l.PointsEarned;
                    p["PointsUsed"] = l.PointsUsed;
                    p["TransactionType"] = l.TransactionType;
                    p["Date"] = l.Date;
                    p["IsDeleted"] = l.IsDeleted;
                    break;

                case Feedback f:
                    p["CustomerRowGuid"] = await GuidOfAsync(db, "Customer", f.CustomerId, cache, ct);
                    p["Type"] = f.Type;
                    p["Category"] = f.Category;
                    p["Comment"] = f.Comment;
                    p["DateSubmitted"] = f.DateSubmitted;
                    p["Status"] = f.Status;
                    p["IsDeleted"] = f.IsDeleted;
                    p["BranchRowGuid"] = f.BranchId == null
                        ? null
                        : await GuidOfAsync(db, "Branch", f.BranchId.Value, cache, ct);
                    break;

                case Branch b:
                    p["BranchName"] = b.BranchName;
                    p["Address"] = b.Address;
                    p["Status"] = b.Status;
                    p["CreatedAt"] = b.CreatedAt;
                    break;

                case BranchInventory bi:
                    p["BranchRowGuid"] = await GuidOfAsync(db, "Branch", bi.BranchId, cache, ct);
                    p["ProductRowGuid"] = await GuidOfAsync(db, "Product", bi.ProductId, cache, ct);
                    p["Quantity"] = bi.Quantity;
                    p["QuantityDelta"] = change.Delta;
                    break;

                case BranchAssignment ba:
                    p["UserId"] = ba.UserId;
                    p["UserName"] = ba.UserName;
                    p["BranchRowGuid"] = ba.BranchId == null
                        ? null
                        : await GuidOfAsync(db, "Branch", ba.BranchId.Value, cache, ct);
                    break;

                case Inquiry i:
                    p["CustomerRowGuid"] = await GuidOfAsync(db, "Customer", i.CustomerId, cache, ct);
                    p["Type"] = i.Type;
                    p["Source"] = i.Source;
                    p["Subject"] = i.Subject;
                    p["Message"] = i.Message;
                    p["DateSubmitted"] = i.DateSubmitted;
                    p["Status"] = i.Status;
                    p["Response"] = i.Response;
                    p["RespondedBy"] = i.RespondedBy;
                    p["RespondedAt"] = i.RespondedAt;
                    p["IsDeleted"] = i.IsDeleted;
                    p["BranchRowGuid"] = i.BranchId == null
                        ? null
                        : await GuidOfAsync(db, "Branch", i.BranchId.Value, cache, ct);
                    break;
            }

            return p;
        }

        private static async Task<Guid?> GuidOfAsync(
            TenantCrmDbContext db,
            string type,
            int id,
            Dictionary<string, Guid?> cache,
            CancellationToken ct)
        {
            var key = type + ":" + id;

            if (cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Guid? value = type switch
            {
                "Customer" => await db.Customers.AsNoTracking().Where(x => x.CustomerId == id)
                    .Select(x => (Guid?)x.RowGuid).FirstOrDefaultAsync(ct),
                "Product" => await db.Products.AsNoTracking().Where(x => x.ProductId == id)
                    .Select(x => (Guid?)x.RowGuid).FirstOrDefaultAsync(ct),
                "Promotion" => await db.Promotions.AsNoTracking().Where(x => x.PromotionId == id)
                    .Select(x => (Guid?)x.RowGuid).FirstOrDefaultAsync(ct),
                "SalesTransaction" => await db.SalesTransactions.AsNoTracking().Where(x => x.TransactionId == id)
                    .Select(x => (Guid?)x.RowGuid).FirstOrDefaultAsync(ct),
                "Branch" => await db.Branches.AsNoTracking().Where(x => x.BranchId == id)
                    .Select(x => (Guid?)x.RowGuid).FirstOrDefaultAsync(ct),
                _ => null
            };

            cache[key] = value;
            return value;
        }
    }
}