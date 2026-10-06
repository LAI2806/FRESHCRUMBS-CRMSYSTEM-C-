using freshcrumbs.CRM.domain.entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using freshcrumbs.CRM.domain.entities;

namespace freshcrumbs.CRM.infrastructure.data
{
    public class MasterCrmDbContext : IdentityDbContext<ApplicationUser>
    {
        public MasterCrmDbContext(
            DbContextOptions<MasterCrmDbContext> options)
            : base(options)
        { }
        public DbSet<Company> Companies => Set<Company>();
        public DbSet<CompanyDatabase> CompanyDatabases => Set<CompanyDatabase>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<TermsVersion> TermsVersions => Set<TermsVersion>();
        public DbSet<TermsAcceptance> TermsAcceptances => Set<TermsAcceptance>();
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Company>(entity =>
            {
                entity.HasKey(x => x.CompanyId);

                entity.Property(x => x.CompanyCode)
                  .HasMaxLength(50)
                  .IsRequired();

                entity.Property(x => x.CompanyName)
                  .HasMaxLength(200)
                  .IsRequired();

                entity.Property(x => x.BusinessAddress)
                  .HasMaxLength(300);

                entity.Property(x => x.ContactNo)
                  .HasMaxLength(20);

                entity.Property(x => x.Email)
                  .HasMaxLength(150);

                entity.Property(x => x.Status)
                  .HasMaxLength(20)
                  .IsRequired();

                entity.HasIndex(x => x.CompanyCode)
                  .IsUnique();

            });

            builder.Entity<CompanyDatabase>(entity =>
            {
                entity.HasKey(x => x.CompanyDatabaseId);

                entity.Property(x => x.ServerName)
                  .HasMaxLength(200)
                  .IsRequired();

                entity.Property(x => x.DatabaseName)
                  .HasMaxLength(200)
                  .IsRequired();

                entity.HasOne(x => x.Company)
                  .WithMany()
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);

            });

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(x => x.Company)
                    .WithMany(x => x.Users)
                    .HasForeignKey(x => x.TenantId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Plan>(entity =>
            {
                entity.HasKey(x => x.PlanId);

                entity.Property(x => x.PlanCode)
                  .HasMaxLength(30)
                  .IsRequired();

                entity.Property(x => x.DisplayName)
                  .HasMaxLength(100)
                  .IsRequired();

                entity.Property(x => x.Description)
                  .HasMaxLength(500)
                  .IsRequired();

                entity.Property(x => x.Price)
                  .HasColumnType("decimal(18,2)");

                entity.Property(x => x.BillingCycle)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

                entity.HasIndex(x => x.PlanCode)
                  .IsUnique();

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Plans_Price", "[Price] >= 0");
                    t.HasCheckConstraint("CK_Plans_MaxUsers", "[MaxUsers] >= 1");
                    t.HasCheckConstraint(
                        "CK_Plans_Branching",
                        "([BranchingEnabled] = 1 AND [MaxBranches] >= 1) OR ([BranchingEnabled] = 0 AND [MaxBranches] IS NULL)");
                });
            });

            builder.Entity<PlanFeature>(entity =>
            {
                entity.HasKey(x => new { x.PlanId, x.FeatureKey });

                entity.Property(x => x.FeatureKey)
                  .HasMaxLength(50)
                  .IsRequired();

                entity.HasOne(x => x.Plan)
                  .WithMany(x => x.Features)
                  .HasForeignKey(x => x.PlanId)
                  .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Subscription>(entity =>
            {
                entity.HasKey(x => x.SubscriptionId);

                entity.Property(x => x.PlanCode)
                  .HasMaxLength(30)
                  .IsRequired();

                entity.Property(x => x.PlanName)
                  .HasMaxLength(100)
                  .IsRequired();

                entity.Property(x => x.Price)
                  .HasColumnType("decimal(18,2)");

                entity.Property(x => x.BillingCycle)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

                entity.Property(x => x.Features)
                  .HasMaxLength(300)
                  .IsRequired();

                entity.Property(x => x.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

                entity.Property(x => x.ChangeType)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

                entity.Property(x => x.Reason)
                  .HasMaxLength(500);

                entity.Property(x => x.ChangedBy)
                  .HasMaxLength(256)
                  .IsRequired();

                entity.HasOne(x => x.Company)
                  .WithMany()
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Plan)
                  .WithMany()
                  .HasForeignKey(x => x.PlanId)
                  .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.PreviousSubscription)
                  .WithMany()
                  .HasForeignKey(x => x.PreviousSubscriptionId)
                  .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new { x.CompanyId, x.StartDate });

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Subscriptions_Price", "[Price] >= 0");
                    t.HasCheckConstraint("CK_Subscriptions_MaxUsers", "[MaxUsers] >= 1");
                    t.HasCheckConstraint("CK_Subscriptions_Dates", "[EndDate] >= [StartDate]");
                    t.HasCheckConstraint(
                        "CK_Subscriptions_Branching",
                        "([BranchingEnabled] = 1 AND [MaxBranches] >= 1) OR ([BranchingEnabled] = 0 AND [MaxBranches] IS NULL)");
                });
            });

            builder.Entity<TermsVersion>(entity =>
            {
                entity.HasKey(x => x.TermsVersionId);

                entity.Property(x => x.Title)
                  .HasMaxLength(200)
                  .IsRequired();

                entity.Property(x => x.Content)
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

                entity.Property(x => x.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

                entity.Property(x => x.CreatedBy)
                  .HasMaxLength(256)
                  .IsRequired();

                entity.Property(x => x.PublishedBy)
                  .HasMaxLength(256);

                entity.HasIndex(x => x.VersionNumber)
                  .IsUnique();
            });

            builder.Entity<TermsAcceptance>(entity =>
            {
                entity.HasKey(x => x.TermsAcceptanceId);

                entity.Property(x => x.UserId)
                  .HasMaxLength(450)
                  .IsRequired();

                entity.Property(x => x.UserName)
                  .HasMaxLength(256)
                  .IsRequired();

                entity.HasOne(x => x.TermsVersion)
                  .WithMany()
                  .HasForeignKey(x => x.TermsVersionId)
                  .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Company)
                  .WithMany()
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new { x.CompanyId, x.TermsVersionId })
                  .IsUnique();
            });

        }

        // Published or archived terms are immutable: only the status/date bookkeeping may change.
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            EnforceTermsImmutability();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            EnforceTermsImmutability();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void EnforceTermsImmutability()
        {
            ChangeTracker.DetectChanges();

            foreach (var entry in ChangeTracker.Entries<TermsVersion>())
            {
                if (entry.State != EntityState.Modified && entry.State != EntityState.Deleted)
                {
                    continue;
                }

                var originalStatus = entry.OriginalValues.GetValue<TermsStatus>(nameof(TermsVersion.Status));

                if (originalStatus == TermsStatus.Draft)
                {
                    continue;
                }

                if (entry.State == EntityState.Deleted)
                {
                    throw new InvalidOperationException("Published terms cannot be deleted.");
                }

                if (entry.Property(x => x.Title).IsModified
                    || entry.Property(x => x.Content).IsModified
                    || entry.Property(x => x.VersionNumber).IsModified
                    || entry.Property(x => x.RequiresAcceptance).IsModified)
                {
                    throw new InvalidOperationException("Published terms are immutable. Create a new version instead.");
                }
            }
        }
    }
}