using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectronicStore.Data.Configurations;

public class ShippingAddressConfiguration : IEntityTypeConfiguration<ShippingAddress>
{
    public void Configure(EntityTypeBuilder<ShippingAddress> builder)
    {
        builder.ToTable("ShippingAddresses");

        builder.HasKey(a => a.Id);

        // Ghép từ các cột khác, không lưu.
        builder.Ignore(a => a.FullAddress);

        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.ReceiverName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.PhoneNumber).IsRequired().HasMaxLength(20);

        builder.Property(a => a.ProvinceCode).IsRequired().HasMaxLength(20);
        builder.Property(a => a.ProvinceName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.DistrictCode).HasMaxLength(20);
        builder.Property(a => a.DistrictName).HasMaxLength(100);
        builder.Property(a => a.WardCode).IsRequired().HasMaxLength(20);
        builder.Property(a => a.WardName).IsRequired().HasMaxLength(100);

        builder.Property(a => a.StreetAddress).IsRequired().HasMaxLength(255);

        // Sổ địa chỉ của một tài khoản, mới nhất trước.
        builder.HasIndex(a => new { a.UserId, a.CreatedAt });

        // Chốt chặn ở tầng database cho quy tắc "mỗi tài khoản nhiều nhất một địa chỉ mặc
        // định": nếu tầng ứng dụng có sót nhánh nào thì lệnh ghi sẽ hỏng chứ không âm thầm
        // tạo ra hai địa chỉ mặc định.
        builder.HasIndex(a => a.UserId, "UX_ShippingAddresses_UserId_Default")
            .IsUnique()
            .HasFilter("[IsDefault] = 1");

        // Cascade: một mục sổ địa chỉ chỉ thuộc về đúng một tài khoản. Đơn hàng không bị
        // ảnh hưởng vì đơn đã snapshot thông tin giao hàng của riêng nó.
        builder.HasOne(a => a.User)
            .WithMany(u => u.ShippingAddresses)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
