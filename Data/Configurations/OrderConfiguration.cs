using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectronicStore.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_SubTotal", "[SubTotal] >= 0");
            t.HasCheckConstraint("CK_Orders_ShippingFee", "[ShippingFee] >= 0");
            t.HasCheckConstraint("CK_Orders_TotalAmount", "[TotalAmount] >= 0");
        });

        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderCode).IsRequired().HasMaxLength(20);
        builder.Property(o => o.UserId).IsRequired();

        // Shipping snapshot.
        builder.Property(o => o.ShippingFullName).IsRequired().HasMaxLength(100);
        builder.Property(o => o.ShippingPhone).IsRequired().HasMaxLength(20);
        builder.Property(o => o.ShippingAddress).IsRequired().HasMaxLength(500);

        builder.Property(o => o.SubTotal).HasPrecision(18, 2);
        builder.Property(o => o.ShippingFee).HasPrecision(18, 2);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);

        // Explicit so the storage type does not silently change if the enum is edited.
        builder.Property(o => o.Status).HasConversion<int>();

        builder.Property(o => o.Note).HasMaxLength(500);

        builder.HasIndex(o => o.OrderCode).IsUnique();

        // Order history page.
        builder.HasIndex(o => new { o.UserId, o.CreatedAt });

        // Admin order management, filtered by status.
        builder.HasIndex(o => new { o.Status, o.CreatedAt });

        // Restrict: deleting an account must never erase its order history. Accounts are
        // disabled with IsActive instead.
        builder.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull: the customer may delete the address book entry later; the order keeps
        // its own copy of the shipping details, so only the back-reference is cleared.
        // Cột vẫn tên AddressId để không phải đổi tên cột khi gộp hai model địa chỉ.
        builder.HasOne(o => o.SavedAddress)
            .WithMany()
            .HasForeignKey(o => o.AddressId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
