using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;

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

                entity.Property(x => x.Status)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.HasIndex(x => x.ProductCode)
                    .IsUnique();
            });
            builder.Entity<Customer>(entity =>
            {
                entity.HasKey(x => x.CustomerId);

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
        }
    }
}