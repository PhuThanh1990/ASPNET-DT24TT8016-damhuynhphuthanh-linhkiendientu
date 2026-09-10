using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectronicStore.Data.Configurations;

public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.ToTable("OrderDetails", t =>
            t.HasCheckConstraint("CK_OrderDetails_Quantity", "[Quantity] > 0"));

        builder.HasKey(d => d.Id);

        // Snapshot of the product at checkout time.
        builder.Property(d => d.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(d => d.UnitPrice).HasPrecision(18, 2);
        builder.Property(d => d.LineTotal).HasPrecision(18, 2);

        // A product appears once per order; buying more increases Quantity.
        builder.HasIndex(d => new { d.OrderId, d.ProductId }).IsUnique();

        // Best seller statistics.
        builder.HasIndex(d => d.ProductId);

        // Cascade: a line has no meaning without its order.
        builder.HasOne(d => d.Order)
            .WithMany(o => o.OrderDetails)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a product that has ever been sold cannot be hard-deleted, otherwise the
        // order history would lose its link. Products are hidden with IsActive instead.
        builder.HasOne(d => d.Product)
            .WithMany(p => p.OrderDetails)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
