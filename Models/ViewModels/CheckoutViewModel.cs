using System.ComponentModel.DataAnnotations;
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

    [Display(Name = "Phường/Xã")]
    [Required(ErrorMessage = "Vui lòng nhập phường/xã.")]
    [StringLength(60, ErrorMessage = "Phường/xã tối đa {1} ký tự.")]
    public string Ward { get; set; } = string.Empty;

    [Display(Name = "Quận/Huyện")]
    [Required(ErrorMessage = "Vui lòng nhập quận/huyện.")]
    [StringLength(60, ErrorMessage = "Quận/huyện tối đa {1} ký tự.")]
    public string District { get; set; } = string.Empty;

    [Display(Name = "Tỉnh/Thành phố")]
    [Required(ErrorMessage = "Vui lòng nhập tỉnh/thành phố.")]
    [StringLength(60, ErrorMessage = "Tỉnh/thành phố tối đa {1} ký tự.")]
    public string Province { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ trong sổ mà khách chọn, hoặc null khi khách tự nhập địa chỉ mới.
    /// Chỉ là con trỏ: tên, số điện thoại và địa chỉ vẫn do <c>IOrderService</c> đọc lại từ
    /// database sau khi kiểm tra quyền sở hữu, nên sửa giá trị này trên form cũng không
    /// dùng được địa chỉ của tài khoản khác.
    /// </summary>
    [Display(Name = "Địa chỉ đã lưu")]
    public int? SelectedAddressId { get; set; }

    /// <summary>Sổ địa chỉ của khách, chỉ để hiển thị.</summary>
    [BindNever]
    [ValidateNever]
    public IReadOnlyList<ShippingAddressListItemViewModel> SavedAddresses { get; set; } = [];

    /// <summary>True khi khách đang chọn một địa chỉ có sẵn thay vì nhập tay.</summary>
    public bool UsesSavedAddress => SelectedAddressId.HasValue;

    [Display(Name = "Ghi chú")]
    [StringLength(500, ErrorMessage = "Ghi chú tối đa {1} ký tự.")]
    public string? Note { get; set; }

    /// <summary>Giỏ hàng đã đối chiếu database, chỉ để hiển thị.</summary>
    [BindNever]
    [ValidateNever]
    public CartViewModel Cart { get; set; } = new();

    /// <summary>Phí vận chuyển do <c>IOrderService.QuoteShippingFee</c> báo, không nhận từ form.</summary>
    [BindNever]
    [ValidateNever]
    public decimal ShippingFee { get; set; }

    public decimal SubTotal => Cart.SubTotal;

    public decimal Total => Cart.SubTotal + ShippingFee;

    /// <summary>
    /// Bốn phần địa chỉ gộp thành một dòng để lưu vào <c>Order.ShippingAddress</c>, đúng
    /// định dạng mà <see cref="ShippingAddress.FullAddress"/> dùng cho sổ địa chỉ.
    /// </summary>
    public string BuildShippingAddress() =>
        string.Join(", ", new[] { AddressLine, Ward, District, Province }
            .Select(part => part?.Trim() ?? string.Empty)
            .Where(part => part.Length > 0));

    /// <summary>Bốn ô địa chỉ đều hợp lệ riêng lẻ vẫn có thể vượt độ dài cột khi gộp lại.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Chọn địa chỉ trong sổ thì các ô nhập tay không được dùng tới.
        if (UsesSavedAddress)
        {
            yield break;
        }

        if (BuildShippingAddress().Length > MaxShippingAddressLength)
        {
            yield return new ValidationResult(
                $"Địa chỉ giao hàng quá dài (tối đa {MaxShippingAddressLength} ký tự).",
                [nameof(AddressLine)]);
        }
    }
}
