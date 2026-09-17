using System.ComponentModel.DataAnnotations;

namespace ElectronicStore.Models.ViewModels;

/// <summary>ADDR-01 — form thêm/sửa một địa chỉ trong sổ địa chỉ.</summary>
/// <remarks>
/// Không có <c>UserId</c>: chủ sở hữu luôn lấy từ cookie đăng nhập ở controller, không bao
/// giờ nhận từ form. <see cref="Id"/> chỉ dùng để đối chiếu với id trên route khi sửa.
/// </remarks>
public class ShippingAddressFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên người nhận.")]
    [StringLength(100, ErrorMessage = "Tên người nhận tối đa {1} ký tự.")]
    [Display(Name = "Người nhận")]
    public string ReceiverName { get; set; } = string.Empty;

    // Cho phép nhập kèm khoảng trắng / dấu chấm / gạch ngang cho dễ gõ; controller sẽ bỏ
    // các ký tự đó trước khi lưu để trong database chỉ còn chữ số.
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(20, ErrorMessage = "Số điện thoại tối đa {1} ký tự.")]
    [RegularExpression(@"^(0|\+84)[0-9.\-\s]{8,13}$",
        ErrorMessage = "Số điện thoại không hợp lệ. Ví dụ: 0901234567 hoặc +84901234567.")]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã Tỉnh/Thành phố.")]
    [StringLength(20, ErrorMessage = "Mã Tỉnh/Thành phố tối đa {1} ký tự.")]
    [Display(Name = "Mã Tỉnh/Thành phố")]
    public string ProvinceCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập Tỉnh/Thành phố.")]
    [StringLength(100, ErrorMessage = "Tỉnh/Thành phố tối đa {1} ký tự.")]
    [Display(Name = "Tỉnh/Thành phố")]
    public string ProvinceName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Mã Quận/Huyện tối đa {1} ký tự.")]
    [Display(Name = "Mã Quận/Huyện")]
    public string? DistrictCode { get; set; }

    [StringLength(100, ErrorMessage = "Quận/Huyện tối đa {1} ký tự.")]
    [Display(Name = "Quận/Huyện")]
    public string? DistrictName { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã Phường/Xã.")]
    [StringLength(20, ErrorMessage = "Mã Phường/Xã tối đa {1} ký tự.")]
    [Display(Name = "Mã Phường/Xã")]
    public string WardCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập Phường/Xã.")]
    [StringLength(100, ErrorMessage = "Phường/Xã tối đa {1} ký tự.")]
    [Display(Name = "Phường/Xã")]
    public string WardName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ cụ thể.")]
    [StringLength(255, ErrorMessage = "Địa chỉ cụ thể tối đa {1} ký tự.")]
    [Display(Name = "Địa chỉ cụ thể")]
    public string StreetAddress { get; set; } = string.Empty;

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
}

/// <summary>Một dòng trong trang danh sách sổ địa chỉ.</summary>
public class ShippingAddressListItemViewModel
{
    public int Id { get; set; }

    public string ReceiverName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string FullAddress { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Trang "Sổ địa chỉ".</summary>
public class ShippingAddressListViewModel
{
    public IReadOnlyList<ShippingAddressListItemViewModel> Addresses { get; set; } = [];

    public bool IsEmpty => Addresses.Count == 0;
}
