using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectronicStore.Data.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Slug).IsRequired().HasMaxLength(120);
        builder.Property(b => b.Description).HasMaxLength(500);
        builder.Property(b => b.LogoUrl).HasMaxLength(500);

        builder.HasIndex(b => b.Slug).IsUnique();
        builder.HasIndex(b => b.Name).IsUnique();

        // The Brand -> Product side of the relationship is configured in
        // ProductConfiguration so that both product foreign keys sit in one place.
    }
}
