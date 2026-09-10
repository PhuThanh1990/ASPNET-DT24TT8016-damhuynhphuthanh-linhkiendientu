using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectronicStore.Data.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");

        builder.HasKey(a => a.Id);

        // Composed from the four address columns, not stored.
        builder.Ignore(a => a.FullAddress);

        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.FullName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.PhoneNumber).IsRequired().HasMaxLength(20);
        builder.Property(a => a.AddressLine).IsRequired().HasMaxLength(255);
        builder.Property(a => a.Ward).IsRequired().HasMaxLength(100);
        builder.Property(a => a.District).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Province).IsRequired().HasMaxLength(100);

        // Address book of one user.
        builder.HasIndex(a => a.UserId);

        // A user has at most one default address.
        builder.HasIndex(a => a.UserId, "UX_Addresses_UserId_Default")
            .IsUnique()
            .HasFilter("[IsDefault] = 1");

        // Cascade is safe: an address book entry belongs to exactly one account. Orders
        // survive because they snapshot the address instead of pointing at it.
        builder.HasOne(a => a.User)
            .WithMany(u => u.Addresses)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
