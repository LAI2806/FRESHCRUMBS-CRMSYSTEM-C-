using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.Services;

namespace freshcrumbs.CRM.infrastructure.data
{
    public class TenantCrmDbContext : DbContext
    {
        public TenantCrmDbContext(
            DbContextOptions<TenantCrmDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();

        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<CustomerDiscountEligibility> CustomerDiscountEligibilities => Set<CustomerDiscountEligibility>();

        public DbSet<Promotion> Promotions => Set<Promotion>();

        public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

        public DbSet<Feedback> Feedbacks => Set<Feedback>();
        public DbSet<Inquiry> Inquiries => Set<Inquiry>();

        public DbSet<SalesTransaction> SalesTransactions => Set<SalesTransaction>();

        public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();

        public DbSet<Branch> Branches => Set<Branch>();

        public DbSet<BranchInventory> BranchInventories => Set<BranchInventory>();

        public DbSet<BranchAssignment> BranchAssignments => Set<BranchAssignment>();

        // ---- Offline-first synchronization ----
        public DbSet<SyncOutboxEntry> SyncOutbox => Set<SyncOutboxEntry>();

        public DbSet<SyncReceipt> SyncReceipts => Set<SyncReceipt>();

        public DbSet<SyncState> SyncStates => Set<SyncState>();

        // True only for the LOCAL (desktop) database: every change is also written to SyncOutbox
        // in the same transaction. Always false for the cloud database.
        public bool CaptureSyncChanges { get; set; }

        // True while the sync engine applies data pulled from the cloud: UpdatedAt is preserved
        // and nothing is written to the outbox (otherwise pulled data would be pushed back).
        public bool ApplyingSync { get; set; }

        // All changes saved through this context instance (= one API request) share this id and are
        // applied as one unit in the cloud.
        public Guid SyncGroupId { get; } = Guid.NewGuid();

        private bool _savingOutbox;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Product>(entity =>
            {
                entity.HasKey(x => x.ProductId);

                entity.Property(x => x.ProductCode)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.ProductName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Category)
                    .HasMaxLength(100);

                entity.Property(x => x.Description)
                    .HasMaxLength(500);

                entity.Property(x => x.Price)
                    .HasPrecision(18, 2);

                entity.Property(x => x.ReorderLevel)
                    .HasDefaultValue(10)
                    .HasSentinel(-1);

                entity.Ignore(x => x.Sold);

                entity.Ignore(x => x.BranchQuantity);

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.HasIndex(x => x.ProductCode)
                    .IsUnique();
            });
            builder.Entity<Customer>(entity =>
            {
                entity.HasKey(x => x.CustomerId);

                entity.Ignore(x => x.BranchNames);

                entity.HasOne<Branch>()
                    .WithMany()
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.CustomerCode)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.FirstName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.LastName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.Email)
                    .HasMaxLength(150);

                entity.Property(x => x.ContactNo)
                    .HasMaxLength(20);

                entity.Property(x => x.Address)
                    .HasMaxLength(300);

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.HasIndex(x => x.CustomerCode)
                    .IsUnique();
            });
            builder.Entity<CustomerDiscountEligibility>(entity =>
            {
                entity.HasKey(x => x.EligibilityId);

                entity.Property(x => x.Category)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.IdNumber)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.VerificationStatus)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.HasOne(x => x.Customer)
                    .WithMany(x => x.DiscountEligibilities)
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new { x.CustomerId, x.Category })
                    .IsUnique();
            });
            builder.Entity<Promotion>(entity =>
            {
                entity.HasKey(x => x.PromotionId);

                entity.HasOne<Branch>()
                    .WithMany()
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.PromotionName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Description)
                    .HasMaxLength(500);

                entity.Property(x => x.DiscountType)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.DiscountValue)
                    .HasPrecision(18, 2);

                entity.Property(x => x.MinimumPurchase)
                    .HasPrecision(18, 2);

                entity.Property(x => x.RequiredLoyaltyPoints)
                    .HasDefaultValue(0);

                entity.Property(x => x.EligibilityCategory)
                    .HasMaxLength(50);

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired();
            });
            builder.Entity<LoyaltyTransaction>(entity =>
            {
                entity.HasKey(x => x.LoyaltyTransactionId);

                entity.Property(x => x.TransactionType)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.IsDeleted)
                    .HasDefaultValue(false);

                entity.HasOne(x => x.Customer)
                                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.SalesTransaction)
                    .WithMany()
                    .HasForeignKey(x => x.SalesTransactionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
            builder.Entity<Feedback>(entity =>
            {
                entity.HasKey(x => x.FeedbackId);

                entity.Ignore(x => x.BranchName);

                entity.HasOne<Branch>()
                    .WithMany()
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.Type)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.Category)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Comment)
                    .HasMaxLength(1000);

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.IsDeleted)
                    .HasDefaultValue(false);

                entity.HasOne(x => x.Customer)
                                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<SalesTransaction>(entity =>
            {
                entity.HasKey(x => x.TransactionId);

                entity.Property(x => x.TotalAmount)
                    .HasPrecision(18, 2);

                entity.Property(x => x.DiscountAmount)
                    .HasPrecision(18, 2);

                entity.Property(x => x.CustomerDiscountAmount)
                    .HasPrecision(18, 2);

                entity.Property(x => x.FinalAmount)
                    .HasPrecision(18, 2);

                entity.Property(x => x.PaymentMethod)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.IsDeleted)
                    .HasDefaultValue(false);

                entity.HasOne(x => x.Customer)
                                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Promotion)
                    .WithMany()
                    .HasForeignKey(x => x.PromotionId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne<Branch>()
                    .WithMany()
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Branch>(entity =>
            {
                entity.HasKey(x => x.BranchId);

                entity.Property(x => x.BranchName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.Address)
                    .HasMaxLength(300);

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired();
            });

            builder.Entity<BranchInventory>(entity =>
            {
                entity.HasKey(x => x.BranchInventoryId);

                entity.HasOne(x => x.Branch)
                    .WithMany()
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Product)
                    .WithMany()
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new { x.BranchId, x.ProductId })
                    .IsUnique();
            });

            builder.Entity<BranchAssignment>(entity =>
            {
                entity.HasKey(x => x.BranchAssignmentId);

                entity.Property(x => x.UserId)
                    .HasMaxLength(450)
                    .IsRequired();

                entity.Property(x => x.UserName)
                    .HasMaxLength(256)
                    .IsRequired();

                entity.HasOne(x => x.Branch)
                    .WithMany()
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => x.UserId)
                    .IsUnique();
            });

            builder.Entity<TransactionItem>(entity =>
            {
                entity.HasKey(x => x.TransactionItemId);

                entity.Property(x => x.UnitPrice)
                    .HasPrecision(18, 2);

                entity.Property(x => x.Subtotal)
                    .HasPrecision(18, 2);

                entity.HasOne(x => x.SalesTransaction)
                    .WithMany(x => x.TransactionItems)
                    .HasForeignKey(x => x.TransactionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Product)
                    .WithMany(x => x.TransactionItems)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Inquiry>(entity =>
            {
                entity.HasKey(x => x.InquiryId);

                entity.Ignore(x => x.BranchName);

                entity.HasOne<Branch>()
                    .WithMany()
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.Type)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.Source)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.Subject)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Message)
                    .HasMaxLength(1000)
                    .IsRequired();

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.Response)
                    .HasMaxLength(1000);

                entity.Property(x => x.RespondedBy)
                    .HasMaxLength(100);

                entity.Property(x => x.IsDeleted)
                    .HasDefaultValue(false);

                entity.HasOne(x => x.Customer)
                                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            ConfigureSyncColumns<Product>(builder);
            ConfigureSyncColumns<Customer>(builder);
            ConfigureSyncColumns<CustomerDiscountEligibility>(builder);
            ConfigureSyncColumns<Promotion>(builder);
            ConfigureSyncColumns<LoyaltyTransaction>(builder);
            ConfigureSyncColumns<Feedback>(builder);
            ConfigureSyncColumns<Inquiry>(builder);
            ConfigureSyncColumns<SalesTransaction>(builder);
            ConfigureSyncColumns<TransactionItem>(builder);
            ConfigureSyncColumns<Branch>(builder);
            ConfigureSyncColumns<BranchInventory>(builder);
            ConfigureSyncColumns<BranchAssignment>(builder);

            builder.Entity<SyncOutboxEntry>(entity =>
            {
                entity.HasKey(x => x.Sequence);

                entity.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Operation).HasMaxLength(20).IsRequired();
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.Property(x => x.Payload).HasColumnType("nvarchar(max)").IsRequired();
                entity.Property(x => x.LastError).HasMaxLength(1000);

                entity.HasIndex(x => x.OperationId).IsUnique();
                entity.HasIndex(x => new { x.Status, x.Sequence });
                entity.HasIndex(x => x.EntityRowGuid);
            });

            builder.Entity<SyncReceipt>(entity =>
            {
                entity.HasKey(x => x.OperationId);

                entity.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Result).HasMaxLength(20).IsRequired();
                entity.Property(x => x.Detail).HasMaxLength(500);

                entity.HasIndex(x => x.ProcessedAtUtc);
            });

            builder.Entity<SyncState>(entity =>
            {
                entity.HasKey(x => x.Key);

                entity.Property(x => x.Key).HasMaxLength(100);
                entity.Property(x => x.Value).HasMaxLength(2000).IsRequired();
            });
        }

        // RowGuid: global identity (unique). The SQL defaults give every EXISTING row a value
        // when the AddOfflineSync migration is applied to a database that already contains data.
        private static void ConfigureSyncColumns<T>(ModelBuilder builder) where T : class, ISyncEntity
        {
            builder.Entity<T>(entity =>
            {
                entity.Property(x => x.RowGuid).HasDefaultValueSql("NEWID()");
                entity.Property(x => x.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasIndex(x => x.RowGuid).IsUnique();
            });
        }

        // ------------------------------------------------------------------
        // SaveChanges: stamp UpdatedAt and (local mode) write the outbox atomically.
        // ------------------------------------------------------------------
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            if (CaptureSyncChanges && !ApplyingSync)
            {
                throw new InvalidOperationException(
                    "Local sync capture requires SaveChangesAsync.");
            }

            StampSyncEntities();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override async Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            if (_savingOutbox)
            {
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            var captured = StampSyncEntities();

            if (!CaptureSyncChanges || ApplyingSync || captured.Count == 0)
            {
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            // Local database: data change + outbox rows commit together or not at all.
            // (The local context deliberately has no retry strategy, so a user transaction is allowed.)
            IDbContextTransaction? ownTransaction = null;

            if (Database.CurrentTransaction == null)
            {
                ownTransaction = await Database.BeginTransactionAsync(cancellationToken);
            }

            try
            {
                var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

                // Keys are known now, so parents can be referenced by RowGuid.
                var rows = await SyncPayloadBuilder.BuildOutboxRowsAsync(
                    this, captured, SyncGroupId, cancellationToken);

                _savingOutbox = true;
                try
                {
                    SyncOutbox.AddRange(rows);
                    await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                }
                finally
                {
                    _savingOutbox = false;
                }

                if (ownTransaction != null)
                {
                    await ownTransaction.CommitAsync(cancellationToken);
                }

                return result;
            }
            finally
            {
                if (ownTransaction != null)
                {
                    await ownTransaction.DisposeAsync();
                }
            }
        }

        // Sets UpdatedAt on added/modified sync entities and returns what has to be captured.
        private List<CapturedSyncChange> StampSyncEntities()
        {
            ChangeTracker.DetectChanges();

            var now = DateTime.UtcNow;
            var captured = new List<CapturedSyncChange>();

            foreach (var entry in ChangeTracker.Entries().ToList())
            {
                if (entry.Entity is not ISyncEntity syncEntity)
                {
                    continue;
                }

                if (entry.State != EntityState.Added
                    && entry.State != EntityState.Modified
                    && entry.State != EntityState.Deleted)
                {
                    continue;
                }

                if (!ApplyingSync && entry.State != EntityState.Deleted)
                {
                    syncEntity.UpdatedAt = now;
                }

                if (!CaptureSyncChanges || ApplyingSync)
                {
                    continue;
                }

                var change = new CapturedSyncChange
                {
                    Entity = syncEntity,
                    State = entry.State,
                    RowGuid = syncEntity.RowGuid
                };

                // Quantity / loyalty points are synchronized as DELTAS so that sales made on several
                // devices add up instead of overwriting each other.
                if (entry.State == EntityState.Modified && entry.Entity is Product)
                {
                    var p = entry.Property(nameof(Product.Quantity));
                    change.Delta = (int)p.CurrentValue! - (int)p.OriginalValue!;
                }
                else if (entry.State == EntityState.Modified && entry.Entity is BranchInventory)
                {
                    var p = entry.Property(nameof(BranchInventory.Quantity));
                    change.Delta = (int)p.CurrentValue! - (int)p.OriginalValue!;
                }
                else if (entry.State == EntityState.Modified && entry.Entity is Customer)
                {
                    var p = entry.Property(nameof(Customer.LoyaltyPoints));
                    change.Delta = (int)p.CurrentValue! - (int)p.OriginalValue!;
                }

                captured.Add(change);
            }

            return captured;
        }
    }
}