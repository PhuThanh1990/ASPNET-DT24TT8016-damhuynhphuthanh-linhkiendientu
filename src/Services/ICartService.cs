using ElectronicStore.Models.ViewModels;

namespace ElectronicStore.Services;

/// <summary>
/// Đọc/kiểm tra giỏ hàng đang nằm trong Session (CUS-12 → CUS-15).
/// Tách khỏi <c>CartController</c> vì trang Checkout (CUS-16) cần đúng logic đối chiếu
/// database này — để mỗi controller tự viết lại là nguồn gốc của sai lệch.
/// </summary>
/// <remarks>
/// Service này KHÔNG đặt hàng: tạo đơn, trừ tồn kho và tính tiền là việc của
/// <see cref="IOrderService"/>. Ở đây chỉ có trạng thái tạm của giỏ hàng và giá hiển thị.
/// </remarks>
public interface ICartService
{
    /// <summary>
    /// Giỏ hàng đã đối chiếu với database: làm mới tên/ảnh/giá, kẹp số lượng theo tồn kho
    /// và bỏ sản phẩm không còn bán. Session được ghi lại nếu có thay đổi, và mọi chỉnh sửa
    /// đều được liệt kê trong <see cref="CartViewModel.Notices"/>.
    /// </summary>
    Task<CartViewModel> BuildAsync(ISession session, CancellationToken cancellationToken = default);

    /// <summary>Ảnh chụp một sản phẩm đang được bán; null nếu không tồn tại hoặc đã ẩn.</summary>
    Task<CartProduct?> FindSellableAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Xóa sạch giỏ hàng. Chỉ gọi sau khi đơn hàng đã được tạo thành công.</summary>
    void Clear(ISession session);
}

/// <summary>
/// Dữ liệu sản phẩm lấy thẳng từ database — nguồn sự thật duy nhất cho tên, ảnh, giá và
/// tồn kho hiển thị trong giỏ hàng.
/// </summary>
public sealed record CartProduct(
    int Id,
    string Name,
    string Slug,
    string? ImageUrl,
    decimal Price,
    int StockQuantity);
