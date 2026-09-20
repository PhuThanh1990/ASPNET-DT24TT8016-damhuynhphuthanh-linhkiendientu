using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectronicStore.Data.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(i => i.AltText).HasMaxLength(200);

        // Gallery lookup: all images of a product in display order. Also covers the
        // foreign key, so EF does not add a separate IX_ProductImages_ProductId.
        builder.HasIndex(i => new { i.ProductId, i.SortOrder });

        // Filtered unique index: a product can have at most one primary image.
        builder.HasIndex(i => i.ProductId)
            .IsUnique()
            .HasFilter("[IsPrimary] = 1")
            .HasDatabaseName("UX_ProductImages_ProductId_Primary");

        // Cascade is safe here: an image row has no meaning without its product.
        builder.HasOne(i => i.Product)
            .WithMany(p => p.ProductImages)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
