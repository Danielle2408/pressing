using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pressing.Models;

namespace Pressing.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<GarmentCategory> GarmentCategories => Set<GarmentCategory>();
    public DbSet<LaundryService> LaundryServices => Set<LaundryService>();
    public DbSet<ServiceTariff> ServiceTariffs => Set<ServiceTariff>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<GarmentCategory>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => item.NormalizedName).IsUnique();
        });

        builder.Entity<LaundryService>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => item.NormalizedName).IsUnique();
        });

        builder.Entity<ServiceTariff>(entity =>
        {
            entity.Property(item => item.Price).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.GarmentCategoryId, item.LaundryServiceId, item.ValidFromUtc })
                .IsUnique();
            entity.HasIndex(item => new { item.GarmentCategoryId, item.LaundryServiceId, item.ValidUntilUtc });
            entity.ToTable(table => table.HasCheckConstraint("CK_ServiceTariffs_Price_NonNegative", "\"Price\" >= 0"));
            entity.HasOne(item => item.GarmentCategory)
                .WithMany(category => category.Tariffs)
                .HasForeignKey(item => item.GarmentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LaundryService)
                .WithMany(service => service.Tariffs)
                .HasForeignKey(item => item.LaundryServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
