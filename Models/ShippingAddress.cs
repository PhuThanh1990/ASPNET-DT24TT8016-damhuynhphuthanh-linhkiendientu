namespace ElectronicStore.Models;

/// <summary>
/// ADDR-01 — một mục trong sổ địa chỉ giao hàng của khách. Một tài khoản lưu được nhiều
/// địa chỉ, trong đó nhiều nhất một địa chỉ mang cờ <see cref="IsDefault"/>.
/// Mapping nằm ở <c>Data/Configurations/ShippingAddressConfiguration.cs</c>.
/// </summary>
/// <remarks>
/// Lưu song song cả mã (<c>*Code</c>) lẫn tên (<c>*Name</c>) của đơn vị hành chính: mã để
/// nối với nguồn dữ liệu tỉnh/phường sau này, tên để hiển thị và để chụp lại (snapshot)
/// vào đơn hàng mà không phải tra cứu lại. Quận/Huyện để nullable vì địa chỉ hai cấp
/// (tỉnh → phường/xã) là hợp lệ.
/// </remarks>
public class ShippingAddress
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>Người nhận, có thể khác chủ tài khoản.</summary>
    public string ReceiverName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string ProvinceCode { get; set; } = string.Empty;

    public string ProvinceName { get; set; } = string.Empty;

    public string? DistrictCode { get; set; }

    public string? DistrictName { get; set; }

    public string WardCode { get; set; } = string.Empty;

    public string WardName { get; set; } = string.Empty;

    /// <summary>Số nhà, tên đường, thôn/xóm.</summary>
    public string StreetAddress { get; set; } = string.Empty;

    /// <summary>Địa chỉ được chọn sẵn khi đặt hàng. Tối đa một địa chỉ mỗi tài khoản.</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Các phần địa chỉ ghép thành một dòng để hiển thị. Không phải cột trong database —
    /// bỏ qua phần Quận/Huyện khi địa chỉ chỉ có hai cấp.
    /// </summary>
    public string FullAddress => string.Join(", ", new[]
    {
        StreetAddress,
        WardName,
        DistrictName,
        ProvinceName
    }.Where(part => !string.IsNullOrWhiteSpace(part)));

    public ApplicationUser User { get; set; } = null!;
}
