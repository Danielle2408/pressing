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
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LaundryOrder> LaundryOrders => Set<LaundryOrder>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<CashClosure> CashClosures => Set<CashClosure>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<CustomerClaim> CustomerClaims => Set<CustomerClaim>();

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

        builder.Entity<Customer>(entity =>
        {
            entity.Property(item => item.FullName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(item => item.NormalizedPhone).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Address).HasMaxLength(400);
            entity.HasIndex(item => item.NormalizedPhone);
            entity.HasOne(item => item.ApplicationUser)
                .WithOne()
                .HasForeignKey<Customer>(item => item.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<LaundryOrder>(entity =>
        {
            entity.Property(item => item.Number).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.Property(item => item.AmountPaid).HasPrecision(18, 2);
            entity.Property(item => item.ExpressSurcharge).HasPrecision(18, 2);
            entity.Property(item => item.CollectionFee).HasPrecision(18, 2);
            entity.Property(item => item.DeliveryFee).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.CancellationReason).HasMaxLength(500);
            entity.Property(item => item.CollectionAddress).HasMaxLength(400);
            entity.Property(item => item.DeliveryAddress).HasMaxLength(400);
            entity.HasIndex(item => item.Number).IsUnique();
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.CustomerId);
            entity.HasIndex(item => item.CreatedAtUtc);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_LaundryOrders_Total_NonNegative", "\"TotalAmount\" >= 0");
                table.HasCheckConstraint("CK_LaundryOrders_AmountPaid_NonNegative", "\"AmountPaid\" >= 0");
            });
            entity.HasOne(item => item.Customer).WithMany(customer => customer.Orders)
                .HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CreatedByUser).WithMany()
                .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrderLine>(entity =>
        {
            entity.Property(item => item.CategoryName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ServiceName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.LineTotal).HasPrecision(18, 2);
            entity.Property(item => item.Color).HasMaxLength(60);
            entity.Property(item => item.Brand).HasMaxLength(80);
            entity.Property(item => item.ReceptionCondition).HasMaxLength(500);
            entity.Property(item => item.Notes).HasMaxLength(500);
            entity.HasIndex(item => item.LaundryOrderId);
            entity.HasOne(item => item.LaundryOrder).WithMany(order => order.Lines)
                .HasForeignKey(item => item.LaundryOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ServiceTariff).WithMany()
                .HasForeignKey(item => item.ServiceTariffId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OrderStatusHistory>(entity =>
        {
            entity.Property(item => item.PreviousStatus).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.NewStatus).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.Reason).HasMaxLength(500);
            entity.HasIndex(item => new { item.LaundryOrderId, item.ChangedAtUtc });
            entity.HasOne(item => item.LaundryOrder).WithMany(order => order.StatusHistory)
                .HasForeignKey(item => item.LaundryOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.User).WithMany()
                .HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrderPayment>(entity =>
        {
            entity.Property(item => item.ReceiptNumber).HasMaxLength(32);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Method).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.TransactionReference).HasMaxLength(120);
            entity.Property(item => item.ProviderReference).HasMaxLength(160);
            entity.Property(item => item.MerchantReference).HasMaxLength(80).IsRequired();
            entity.Property(item => item.CheckoutUrl).HasMaxLength(2048);
            entity.HasIndex(item => item.MerchantReference).IsUnique();
            entity.HasIndex(item => item.ReceiptNumber).IsUnique();
            entity.HasIndex(item => new { item.Method, item.TransactionReference })
                .IsUnique().HasFilter("\"TransactionReference\" IS NOT NULL");
            entity.HasIndex(item => item.ProviderReference).IsUnique()
                .HasFilter("\"ProviderReference\" IS NOT NULL");
            entity.HasIndex(item => new { item.LaundryOrderId, item.CreatedAtUtc });
            entity.ToTable(table => table.HasCheckConstraint("CK_OrderPayments_Amount_Positive", "\"Amount\" > 0"));
            entity.HasOne(item => item.LaundryOrder).WithMany(order => order.Payments)
                .HasForeignKey(item => item.LaundryOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CreatedByUser).WithMany()
                .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CashClosure>(entity =>
        {
            entity.Property(item => item.ExpectedCash).HasPrecision(18, 2);
            entity.Property(item => item.CountedCash).HasPrecision(18, 2);
            entity.Property(item => item.OtherPayments).HasPrecision(18, 2);
            entity.Property(item => item.Note).HasMaxLength(500);
            entity.HasIndex(item => new { item.OperatorId, item.BusinessDate }).IsUnique();
            entity.HasOne(item => item.Operator).WithMany().HasForeignKey(item => item.OperatorId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_CashClosures_Amounts_NonNegative", "\"ExpectedCash\" >= 0 AND \"CountedCash\" >= 0 AND \"OtherPayments\" >= 0"));
        });

        builder.Entity<Expense>(entity =>
        {
            entity.Property(item => item.Label).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.HasIndex(item => item.SpentAtUtc);
            entity.HasOne(item => item.CreatedByUser).WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_Expenses_Amount_Positive", "\"Amount\" > 0"));
        });

        builder.Entity<InventoryItem>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Unit).HasMaxLength(24).IsRequired();
            entity.Property(item => item.Supplier).HasMaxLength(120);
            entity.Property(item => item.QuantityOnHand).HasPrecision(18, 3);
            entity.Property(item => item.AlertThreshold).HasPrecision(18, 3);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("CK_InventoryItems_Quantities_NonNegative", "\"QuantityOnHand\" >= 0 AND \"AlertThreshold\" >= 0"));
        });

        builder.Entity<StockMovement>(entity =>
        {
            entity.Property(item => item.Direction).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.Note).HasMaxLength(500);
            entity.HasIndex(item => new { item.InventoryItemId, item.CreatedAtUtc });
            entity.ToTable(table => table.HasCheckConstraint("CK_StockMovements_Quantity_Positive", "\"Quantity\" > 0"));
            entity.HasOne(item => item.InventoryItem).WithMany(item => item.Movements).HasForeignKey(item => item.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CustomerClaim>(entity =>
        {
            entity.Property(item => item.Description).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Decision).HasMaxLength(2000);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(item => new { item.LaundryOrderId, item.CreatedAtUtc });
            entity.HasIndex(item => new { item.CustomerId, item.CreatedAtUtc });
            entity.HasOne(item => item.LaundryOrder).WithMany(order => order.Claims).HasForeignKey(item => item.LaundryOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Customer).WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CreatedByUser).WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ResolvedByUser).WithMany().HasForeignKey(item => item.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
