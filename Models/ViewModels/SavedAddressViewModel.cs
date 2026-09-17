namespace ElectronicStore.Models.ViewModels;

/// <summary>
/// ADDR-03 — một mục trong sổ địa chỉ của khách, dựng để hiển thị ở trang thanh toán.
/// </summary>
/// <remarks>
/// Tên thuộc tính đặt theo từ vựng của sổ địa chỉ (ReceiverName / StreetAddress / WardName /
/// ProvinceName) nên view đọc tự nhiên, còn nguồn dữ liệu hiện tại vẫn là entity
/// <see cref="Address"/> sẵn có (FullName / AddressLine / Ward / Province).
///
/// Khi ADDR-01 (entity ShippingAddress) và ADDR-02 (service sổ địa chỉ) được merge, chỉ cần
/// đổi chỗ dựng danh sách này sang service đó — view và controller không phải sửa.
/// </remarks>
public sealed class SavedAddressViewModel
{
    public int Id { get; init; }

    /// <summary>Người nhận, có thể khác chủ tài khoản.</summary>
    public string ReceiverName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>Số nhà, tên đường.</summary>
    public string StreetAddress { get; init; } = string.Empty;

    public string WardName { get; init; } = string.Empty;

    public string DistrictName { get; init; } = string.Empty;

    public string ProvinceName { get; init; } = string.Empty;

    /// <summary>Địa chỉ mặc định của khách; trang thanh toán tự chọn mục này.</summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Các phần địa chỉ gộp thành một dòng, đúng thứ tự mà <see cref="Address.FullAddress"/>
    /// dùng — để dòng hiển thị ở đây khớp với dòng sẽ được lưu vào đơn hàng.
    /// </summary>
    public string FullAddress =>
        string.Join(", ", new[] { StreetAddress, WardName, DistrictName, ProvinceName }
            .Select(part => part?.Trim() ?? string.Empty)
            .Where(part => part.Length > 0));
}
