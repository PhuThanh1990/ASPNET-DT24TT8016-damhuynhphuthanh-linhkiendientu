using System.Text.Json.Serialization;

namespace ElectronicStore.Models.ViewModels;

/// <summary>
/// CUS-13/CUS-15 — giỏ hàng đang hiển thị.
///
/// Giỏ hàng chỉ là trạng thái tạm ở tầng presentation (lưu trong Session).
/// <see cref="CartItemViewModel.UnitPrice"/> là ảnh chụp giá tại thời điểm thêm vào giỏ,
/// chỉ dùng để hiển thị. Khi làm Checkout (CUS-16+), OrderService BẮT BUỘC đọc lại giá và
/// tồn kho từ database rồi mới ghi vào OrderDetail — không được tin giá trong Session.
/// </summary>
public class CartViewModel
{
    public List<CartItemViewModel> Items { get; set; } = [];

    /// <summary>Tổng số lượng sản phẩm, dùng cho badge trên navbar.</summary>
    public int TotalQuantity => Items.Sum(item => item.Quantity);

    /// <summary>Tổng tiền hàng = tổng các LineTotal.</summary>
    public decimal SubTotal => Items.Sum(item => item.LineTotal);

    /// <summary>
    /// Số tiền phải trả. Ở giai đoạn giỏ hàng chưa có phí vận chuyển hay khuyến mãi nên
    /// bằng đúng <see cref="SubTotal"/>; phí ship được tính ở Checkout (Order.ShippingFee).
    /// </summary>
    public decimal Total => SubTotal;

    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// Thông báo khi giỏ hàng phải tự sửa lúc mở trang: sản phẩm bị gỡ bán, hết hàng,
    /// hoặc số lượng vượt tồn kho hiện tại.
    /// </summary>
    public List<string> Notices { get; set; } = [];
}

/// <summary>
/// Một dòng trong giỏ hàng. Đây cũng chính là object được serialize xuống Session, nên
/// chỉ chứa kiểu dữ liệu đơn giản, không có navigation property của EF.
/// </summary>
public class CartItemViewModel
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Để link ngược về trang chi tiết sản phẩm.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    /// <summary>Ảnh chụp giá lúc thêm vào giỏ; được làm mới theo database mỗi lần mở giỏ.</summary>
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    /// <summary>
    /// Tồn kho hiện tại, chỉ để render ô nhập số lượng. Không lưu xuống Session vì tồn
    /// kho thay đổi liên tục — server luôn đọc lại từ database trước khi chấp nhận.
    /// </summary>
    [JsonIgnore]
    public int StockQuantity { get; set; }

    [JsonIgnore]
    public decimal LineTotal => UnitPrice * Quantity;
}
