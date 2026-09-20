using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectronicStore.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(120);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.ImageUrl).HasMaxLength(500);

        builder.HasIndex(c => c.Slug).IsUnique();

        // Menu is built by filtering on IsActive and ordering by SortOrder.
        builder.HasIndex(c => new { c.IsActive, c.SortOrder });

        // Optional one-level nesting ("Linh kiện máy tính" -> "CPU"). Restrict because a
        // self-referencing cascade is rejected by SQL Server, and because a parent that
        // still holds children must not disappear silently.
        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.ChildCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
