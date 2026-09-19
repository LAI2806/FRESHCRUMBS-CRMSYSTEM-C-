using freshcrumbs.CRM.domain.entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
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
                    .OnDelete(DeleteBehavior.Restrict);
            });

        }
    }
}
