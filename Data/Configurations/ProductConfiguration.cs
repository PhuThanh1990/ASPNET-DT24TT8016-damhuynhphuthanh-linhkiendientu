using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectronicStore.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Products_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_Products_OldPrice", "[OldPrice] IS NULL OR [OldPrice] >= 0");
            t.HasCheckConstraint("CK_Products_StockQuantity", "[StockQuantity] >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Slug).IsRequired().HasMaxLength(220);
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(50);
        builder.Property(p => p.ShortDescription).HasMaxLength(500);

        // Description and Specifications stay nvarchar(max) on purpose: long HTML and a
        // JSON spec object have no meaningful upper bound.
        builder.Property(p => p.Description).IsRequired(false);
        builder.Property(p => p.Specifications).IsRequired(false);

        // Money must never be float/double. 18 digits with 2 decimals covers VND prices
        // with room for discount arithmetic.
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.OldPrice).HasPrecision(18, 2);

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.Sku).IsUnique();

        // Covers the product list page: active products of a category within a price range.
        builder.HasIndex(p => new { p.IsActive, p.CategoryId, p.Price });

        // Helps prefix search and name ordering; a leading-wildcard LIKE still scans.
        builder.HasIndex(p => p.Name);

        // Restrict on both foreign keys: deleting a category or a brand must never take
        // its products down with it. Admin hides them with IsActive instead.
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Brand)
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
