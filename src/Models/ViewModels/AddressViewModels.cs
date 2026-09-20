using System.ComponentModel.DataAnnotations;
using ElectronicStore.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ElectronicStore.Models.ViewModels;

/// <summary>ADDR-01 — form thêm/sửa một địa chỉ trong sổ địa chỉ.</summary>
/// <remarks>
/// Ánh xạ 1-1 với <see cref="Address"/> đang có sẵn của project (entity mà
/// <c>Order.AddressId</c>, <c>OrderService</c> và luồng checkout dùng chung) — không có
/// model địa chỉ thứ hai.
///
/// Không có <c>UserId</c>: chủ sở hữu luôn lấy từ cookie đăng nhập ở controller, không bao
/// giờ nhận từ form. <see cref="Id"/> chỉ dùng để đối chiếu với id trên route khi sửa.
///
/// FINAL-ADDR — phần địa giới hành chính dùng đúng cơ chế của ADDR-02 ở trang thanh toán:
/// form chỉ gửi lên <see cref="ProvinceCode"/>/<see cref="WardCode"/>, còn
/// <see cref="Province"/>/<see cref="Ward"/> do controller tra từ
/// <see cref="IAdministrativeUnitService"/> rồi gán đè. Sửa <c>&lt;option&gt;</c> trong
/// DevTools vì vậy không đổi được tên lưu vào database.
///
/// Độ dài các trường đặt đúng bằng độ dài cột trong <c>AddressConfiguration</c> để lỗi hiện
/// ra dưới dạng thông báo thay vì lỗi cắt chuỗi từ SQL Server.
/// </remarks>
public class AddressFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên người nhận.")]
    [StringLength(100, ErrorMessage = "Tên người nhận tối đa {1} ký tự.")]
    [Display(Name = "Người nhận")]
    public string FullName { get; set; } = string.Empty;

    // Cho phép nhập kèm khoảng trắng / dấu chấm / gạch ngang cho dễ gõ; controller sẽ bỏ
    // các ký tự đó trước khi lưu để trong database chỉ còn chữ số.
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(20, ErrorMessage = "Số điện thoại tối đa {1} ký tự.")]
    [RegularExpression(@"^(0|\+84)[0-9.\-\s]{8,13}$",
        ErrorMessage = "Số điện thoại không hợp lệ. Ví dụ: 0901234567 hoặc +84901234567.")]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    // ── FINAL-ADDR: địa giới hành chính ───────────────────────────────────────────
    // Hai ô người dùng thật sự chọn. Khi dataset hỏng (SelectorsAvailable == false) form
    // đổi sang ô nhập chữ, lúc đó controller gỡ [Required] của hai mã này và quay lại kiểm
    // tra Province/Ward — xem ShippingAddressController.ResolveAdministrativeUnits.

    [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành phố.")]
    [Display(Name = "Tỉnh/Thành phố")]
    public string ProvinceCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn Phường/Xã.")]
    [Display(Name = "Phường/Xã")]
    public string WardCode { get; set; } = string.Empty;

    /// <summary>
    /// Tên tỉnh/thành phố. Bình thường do server tra từ <see cref="ProvinceCode"/>; chỉ được
    /// nhập tay khi dataset hỏng (xem <see cref="SelectorsAvailable"/>).
    /// </summary>
    [StringLength(100, ErrorMessage = "Tỉnh/Thành phố tối đa {1} ký tự.")]
    [Display(Name = "Tỉnh/Thành phố")]
    public string Province { get; set; } = string.Empty;

    /// <summary>Tên phường/xã; cùng cơ chế với <see cref="Province"/>.</summary>
    [StringLength(100, ErrorMessage = "Phường/Xã tối đa {1} ký tự.")]
    [Display(Name = "Phường/Xã")]
    public string Ward { get; set; } = string.Empty;

    /// <summary>
    /// Cấp quận/huyện đã bỏ từ 01/07/2025 nên form không còn ô này, và
    /// <see cref="BindNeverAttribute"/> chặn luôn việc client tự post kèm — giá trị ghi vào
    /// database do controller quyết định. Vẫn giữ lại (nullable, không <c>[Required]</c>) để
    /// view model soi đúng cột của <see cref="Address"/> và để địa chỉ cũ mang theo được
    /// quận/huyện của nó khi mở form sửa.
    /// </summary>
    [BindNever]
    [ValidateNever]
    [Display(Name = "Quận/Huyện")]
    public string? District { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ cụ thể.")]
    [StringLength(255, ErrorMessage = "Địa chỉ cụ thể tối đa {1} ký tự.")]
    [Display(Name = "Địa chỉ cụ thể")]
    public string AddressLine { get; set; } = string.Empty;

    [Display(Name = "Đặt làm địa chỉ mặc định")]
    public bool IsDefault { get; set; }

    /// <summary>
    /// True khi đây là địa chỉ đầu tiên của tài khoản: lúc đó nó bắt buộc là mặc định nên
    /// ô tick được khóa lại và giải thích lý do thay vì để người dùng bỏ chọn vô ích.
    /// </summary>
    public bool IsFirstAddress { get; set; }

    /// <summary>
    /// True khi đang sửa chính địa chỉ mặc định: không cho bỏ cờ mặc định ở đây, vì làm vậy
    /// sẽ để tài khoản không còn địa chỉ mặc định nào. Muốn đổi thì đặt địa chỉ khác làm
    /// mặc định ở trang danh sách.
    /// </summary>
    public bool IsCurrentDefault { get; set; }

    // ── Dữ liệu đổ vào form, do controller nạp — không bao giờ nhận từ client ──────

    /// <summary>34 tỉnh/thành phố đổ vào selectbox.</summary>
    [BindNever]
    [ValidateNever]
    public IReadOnlyList<ProvinceOption> Provinces { get; set; } = [];

    /// <summary>
    /// Phường/xã của tỉnh đang chọn. Render sẵn từ server để khi mở lại form (sửa địa chỉ cũ
    /// hoặc vừa lỗi validate) lựa chọn cũ vẫn đúng dù JavaScript chưa chạy.
    /// </summary>
    [BindNever]
    [ValidateNever]
    public IReadOnlyList<WardOption> Wards { get; set; } = [];

    /// <summary>
    /// False khi không nạp được dataset địa giới hành chính. View đổi sang ô nhập chữ để
    /// khách vẫn lưu được địa chỉ thay vì gặp hai selectbox rỗng.
    /// </summary>
    [BindNever]
    [ValidateNever]
    public bool SelectorsAvailable { get; set; } = true;

    /// <summary>
    /// Địa chỉ cũ ghi trước ADDR-02 (chưa có mã) hiển thị nguyên văn ở đầu form, để người
    /// dùng biết mình đang sửa cái gì trước khi chọn lại tỉnh/phường. Rỗng ở mọi trường hợp
    /// khác. Do controller dựng từ bản ghi trong database, không nhận từ form.
    /// </summary>
    [BindNever]
    [ValidateNever]
    public string LegacyLocation { get; set; } = string.Empty;

    /// <summary>True khi bản ghi đang sửa là địa chỉ kiểu cũ và selector đang hoạt động.</summary>
    public bool NeedsReselect => SelectorsAvailable && LegacyLocation.Length > 0;
}

/// <summary>Một dòng trong trang danh sách sổ địa chỉ.</summary>
public class AddressListItemViewModel
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string FullAddress { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Trang "Sổ địa chỉ".</summary>
public class AddressListViewModel
{
    public IReadOnlyList<AddressListItemViewModel> Addresses { get; set; } = [];

    public bool IsEmpty => Addresses.Count == 0;
}
