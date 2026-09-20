namespace ElectronicStore.Services;

/// <summary>
/// ADDR-02 — tra cứu đơn vị hành chính Việt Nam (Tỉnh/Thành phố và Phường/Xã) để đổ vào
/// selectbox của form địa chỉ giao hàng.
/// </summary>
/// <remarks>
/// Dữ liệu đọc từ file JSON kèm theo repo (<c>Data/AdministrativeUnits/</c>) nên demo chạy
/// hoàn toàn offline, không gọi API ngoài. Service được đăng ký singleton và nạp file đúng
/// một lần lúc khởi động.
///
/// Theo cơ cấu hành chính áp dụng từ 01/07/2025, Việt Nam còn 2 cấp: Tỉnh/Thành phố →
/// Phường/Xã. Cấp Quận/Huyện không còn, vì vậy ở đây không có API nào cho cấp đó.
/// </remarks>
public interface IAdministrativeUnitService
{
    /// <summary>
    /// False khi không đọc được dataset. Controller dùng cờ này để báo lỗi tử tế thay vì
    /// dựng một form có select rỗng mà người dùng không hiểu vì sao.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>Phiên bản dataset, ví dụ "v5.1.0". Null khi không đọc được.</summary>
    string? DatasetVersion { get; }

    /// <summary>34 tỉnh/thành phố, sắp xếp A→Z theo tên tiếng Việt.</summary>
    IReadOnlyList<ProvinceOption> GetProvinces();

    /// <summary>
    /// Phường/xã của một tỉnh, sắp xếp A→Z. Mã tỉnh không hợp lệ thì trả về danh sách rỗng
    /// chứ không ném lỗi — người dùng chỉ thấy select trống.
    /// </summary>
    IReadOnlyList<WardOption> GetWards(string? provinceCode);

    /// <summary>Tra một tỉnh theo mã; null nếu mã không tồn tại.</summary>
    ProvinceOption? FindProvince(string? provinceCode);

    /// <summary>
    /// Tra một phường/xã theo mã, đồng thời kiểm tra nó đúng là thuộc tỉnh đã chọn.
    /// Trả null nếu mã sai hoặc phường/xã không nằm trong tỉnh đó.
    /// </summary>
    WardOption? FindWard(string? provinceCode, string? wardCode);
}

/// <summary>Một tỉnh/thành phố. <paramref name="Code"/> là mã 2 chữ số của Tổng cục Thống kê.</summary>
public sealed record ProvinceOption(string Code, string Name);

/// <summary>Một phường/xã. <paramref name="Code"/> là mã 5 chữ số của Tổng cục Thống kê.</summary>
public sealed record WardOption(string Code, string Name, string ProvinceCode);
