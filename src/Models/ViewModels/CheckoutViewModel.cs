using System.ComponentModel.DataAnnotations;
using ElectronicStore.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ElectronicStore.Models.ViewModels;

/// <summary>
/// CUS-16 — form thông tin nhận hàng + phần tóm tắt giỏ hàng của trang Checkout.
/// </summary>
/// <remarks>
/// Chỉ các field thông tin nhận hàng được bind từ form. Giỏ hàng, phí ship và tổng tiền
/// gắn <see cref="BindNeverAttribute"/> nên dù client có post thêm
/// <c>Cart.Items[0].UnitPrice</c> hay <c>ShippingFee</c> thì model binder cũng bỏ qua —
/// server luôn dựng lại từ Session và từ <c>IOrderService</c>.
/// </remarks>
public class CheckoutViewModel : IValidatableObject
{
    /// <summary>Độ dài cột Order.ShippingAddress (xem OrderConfiguration).</summary>
    private const int MaxShippingAddressLength = 500;

    [Display(Name = "Họ tên người nhận")]
    [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận.")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa {1} ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Số điện thoại")]
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(20, ErrorMessage = "Số điện thoại tối đa {1} ký tự.")]
    [RegularExpression(@"^(0|\+84)\d{8,10}$",
        ErrorMessage = "Số điện thoại không hợp lệ (ví dụ: 0912345678).")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Địa chỉ")]
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ (số nhà, tên đường).")]
    [StringLength(200, ErrorMessage = "Địa chỉ tối đa {1} ký tự.")]
    public string AddressLine { get; set; } = string.Empty;

    // ── ADDR-02: địa giới hành chính ──────────────────────────────────────────────
    // Form chỉ gửi lên MÃ. Tên tỉnh/phường do server tra từ dataset rồi gán vào Province và
    // Ward bên dưới, nên sửa <option> trong DevTools cũng không đổi được tên lưu vào đơn.

    [Display(Name = "Tỉnh/Thành phố")]
    [Required(ErrorMessage = "Vui lòng chọn tỉnh/thành phố.")]
    public string ProvinceCode { get; set; } = string.Empty;

    [Display(Name = "Phường/Xã")]
    [Required(ErrorMessage = "Vui lòng chọn phường/xã.")]
    public string WardCode { get; set; } = string.Empty;

    /// <summary>
    /// Tên phường/xã. Bình thường do server tra từ mã; chỉ được nhập tay khi dataset hỏng
    /// (xem <see cref="SelectorsAvailable"/>), lúc đó form rơi về ô nhập chữ.
    /// </summary>
    [Display(Name = "Phường/Xã")]
    [StringLength(100, ErrorMessage = "Phường/xã tối đa {1} ký tự.")]
    public string Ward { get; set; } = string.Empty;

    /// <summary>Tên tỉnh/thành phố; cùng cơ chế với <see cref="Ward"/>.</summary>
    [Display(Name = "Tỉnh/Thành phố")]
    [StringLength(100, ErrorMessage = "Tỉnh/thành phố tối đa {1} ký tự.")]
    public string Province { get; set; } = string.Empty;

    [Display(Name = "Ghi chú")]
    [StringLength(500, ErrorMessage = "Ghi chú tối đa {1} ký tự.")]
    public string? Note { get; set; }

    // ----- ADDR-03: sổ địa chỉ -----

    /// <summary>
    /// Id địa chỉ đã lưu mà khách đang chọn; null nghĩa là nhập tay.
    /// </summary>
    /// <remarks>
    /// Chỉ là *lựa chọn*, không phải dữ liệu tin cậy: controller đối chiếu lại id này với sổ
    /// địa chỉ của chính tài khoản đang đăng nhập, và <c>IOrderService</c> kiểm tra quyền sở
    /// hữu một lần nữa trước khi chụp thông tin vào đơn.
    /// </remarks>
    public int? SelectedAddressId { get; set; }

    /// <summary>True khi khách bấm "Thêm địa chỉ mới" và muốn dùng 6 ô nhập tay.</summary>
    public bool UseNewAddress { get; set; }

    /// <summary>Sổ địa chỉ của khách, do controller nạp — không nhận từ form.</summary>
    [BindNever]
    [ValidateNever]
    public IReadOnlyList<AddressListItemViewModel> SavedAddresses { get; set; } = [];

    public bool HasSavedAddresses => SavedAddresses.Count > 0;

    /// <summary>Địa chỉ đang được chọn trong sổ; null khi nhập tay hoặc chưa chọn.</summary>
    public AddressListItemViewModel? SelectedAddress =>
        SelectedAddressId is { } id ? SavedAddresses.FirstOrDefault(a => a.Id == id) : null;

    /// <summary>
    /// Form nhập tay chỉ hiện khi sổ địa chỉ rỗng hoặc khách chủ động thêm địa chỉ mới.
    /// Khi ẩn, jQuery validate bỏ qua các ô này (mặc định <c>ignore: ":hidden"</c>) nên
    /// không chặn submit; phía server controller cũng gỡ lỗi Required tương ứng.
    /// </summary>
    public bool ShowNewAddressForm => !HasSavedAddresses || UseNewAddress;

    /// <summary>Giỏ hàng đã đối chiếu database, chỉ để hiển thị.</summary>
    [BindNever]
    [ValidateNever]
    public CartViewModel Cart { get; set; } = new();

    /// <summary>Phí vận chuyển do <c>IOrderService.QuoteShippingFee</c> báo, không nhận từ form.</summary>
    [BindNever]
    [ValidateNever]
    public decimal ShippingFee { get; set; }

    /// <summary>Danh sách tỉnh/thành đổ vào selectbox.</summary>
    [BindNever]
    [ValidateNever]
    public IReadOnlyList<ProvinceOption> Provinces { get; set; } = [];

    /// <summary>
    /// Phường/xã của tỉnh đang chọn. Render sẵn từ server để khi mở lại form (validate lỗi
    /// hoặc sửa địa chỉ cũ) select đã có đúng lựa chọn mà không cần chờ JavaScript.
    /// </summary>
    [BindNever]
    [ValidateNever]
    public IReadOnlyList<WardOption> Wards { get; set; } = [];

    /// <summary>
    /// False khi không nạp được dataset địa giới hành chính. View sẽ đổi sang ô nhập chữ để
    /// khách vẫn đặt được hàng thay vì gặp hai selectbox rỗng.
    /// </summary>
    [BindNever]
    [ValidateNever]
    public bool SelectorsAvailable { get; set; } = true;

    public decimal SubTotal => Cart.SubTotal;

    public decimal Total => Cart.SubTotal + ShippingFee;

    /// <summary>
    /// Các phần địa chỉ gộp thành một dòng để lưu vào <c>Order.ShippingAddress</c>, đúng
    /// định dạng mà <see cref="Address.FullAddress"/> dùng cho sổ địa chỉ. Không còn phần
    /// quận/huyện: cấp này đã bỏ từ 01/07/2025.
    /// </summary>
    public string BuildShippingAddress() =>
        string.Join(", ", new[] { AddressLine, Ward, Province }
            .Select(part => part?.Trim() ?? string.Empty)
            .Where(part => part.Length > 0));

    /// <summary>Các ô địa chỉ hợp lệ riêng lẻ vẫn có thể vượt độ dài cột khi gộp lại.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BuildShippingAddress().Length > MaxShippingAddressLength)
        {
            yield return new ValidationResult(
                $"Địa chỉ giao hàng quá dài (tối đa {MaxShippingAddressLength} ký tự).",
                [nameof(AddressLine)]);
        }
    }
}
