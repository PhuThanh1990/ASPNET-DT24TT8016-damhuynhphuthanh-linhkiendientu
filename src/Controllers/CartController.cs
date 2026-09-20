using ElectronicStore.Helpers;
using ElectronicStore.Models;
using ElectronicStore.Models.ViewModels;
using ElectronicStore.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicStore.Controllers;

/// <summary>
/// CUS-12 → CUS-15 — giỏ hàng lưu trong Session.
///
/// Nguyên tắc bảo mật của controller này: client chỉ được gửi lên <c>productId</c> và
/// <c>quantity</c>. Tên, ảnh, giá và tồn kho luôn được đọc lại từ database qua
/// <see cref="ICartService"/> — không bao giờ lấy từ form hay từ Session. Nhờ vậy sửa
/// hidden input trên trình duyệt không đổi được giá.
///
/// Đặt hàng không xảy ra ở đây: xem <c>CheckoutController</c> (CUS-16/CUS-17), nơi gọi
/// Core <see cref="IOrderService"/>.
/// </summary>
public class CartController : Controller
{
    private readonly ICartService _cart;

    public CartController(ICartService cart)
    {
        _cart = cart;
    }

    /// <summary>CUS-13 — trang giỏ hàng. Mỗi lần mở đều đối chiếu lại với database.</summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _cart.BuildAsync(HttpContext.Session, cancellationToken));
    }

    /// <summary>CUS-12 — thêm sản phẩm vào giỏ.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        int productId, int quantity = 1, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        // Số lượng gửi lên có thể là 0, số âm hoặc rất lớn -> kéo về khoảng hợp lệ.
        quantity = Math.Clamp(quantity, 1, CartService.MaxQuantityPerItem);

        var product = await _cart.FindSellableAsync(productId, cancellationToken);

        if (product is null)
        {
            SetMessage("danger", "Sản phẩm không tồn tại hoặc đã ngừng kinh doanh.");
            return Back(returnUrl);
        }

        if (product.StockQuantity <= 0)
        {
            SetMessage("warning", $"\"{product.Name}\" hiện đã hết hàng.");
            return Back(returnUrl);
        }

        var items = CartSession.GetItems(HttpContext.Session);
        var existing = items.FirstOrDefault(item => item.ProductId == product.Id);

        var limit = Math.Min(product.StockQuantity, CartService.MaxQuantityPerItem);
        var currentQuantity = existing?.Quantity ?? 0;
        var wantedQuantity = currentQuantity + quantity;
        var acceptedQuantity = Math.Min(wantedQuantity, limit);

        if (acceptedQuantity <= currentQuantity)
        {
            SetMessage("warning", $"\"{product.Name}\" trong giỏ đã đạt mức tối đa ({limit}).");
            return Back(returnUrl);
        }

        if (existing is null)
        {
            var item = new CartItemViewModel { ProductId = product.Id, Quantity = acceptedQuantity };
            CartService.Apply(item, product);
            items.Add(item);
        }
        else
        {
            // Đã có trong giỏ thì cộng dồn, đồng thời làm mới thông tin theo database.
            CartService.Apply(existing, product);
            existing.Quantity = acceptedQuantity;
        }

        CartSession.SaveItems(HttpContext.Session, items);

        if (acceptedQuantity < wantedQuantity)
        {
            SetMessage("warning",
                $"Chỉ còn {limit} sản phẩm \"{product.Name}\", giỏ hàng đã được đặt ở mức tối đa.");
        }
        else
        {
            SetMessage("success", $"Đã thêm \"{product.Name}\" vào giỏ hàng.");
        }

        return Back(returnUrl);
    }

    /// <summary>
    /// CUS-14 — đổi số lượng một dòng.
    /// Nút "−"/"+" gửi kèm <paramref name="delta"/> và được cộng vào số lượng đang lưu
    /// trong Session (không cộng vào con số client gửi lên), còn ô nhập tay thì gửi
    /// <paramref name="quantity"/>. Số lượng &lt;= 0 nghĩa là xóa dòng đó khỏi giỏ.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        int productId, int quantity, int? delta = null, CancellationToken cancellationToken = default)
    {
        var items = CartSession.GetItems(HttpContext.Session);
        var existing = items.FirstOrDefault(item => item.ProductId == productId);

        if (existing is null)
        {
            SetMessage("danger", "Sản phẩm này không có trong giỏ hàng.");
            return RedirectToAction(nameof(Index));
        }

        if (delta.HasValue)
        {
            quantity = existing.Quantity
                + Math.Clamp(delta.Value, -CartService.MaxQuantityPerItem, CartService.MaxQuantityPerItem);
        }

        if (quantity <= 0)
        {
            items.Remove(existing);
            CartSession.SaveItems(HttpContext.Session, items);
            SetMessage("success", $"Đã xóa \"{existing.Name}\" khỏi giỏ hàng.");
            return RedirectToAction(nameof(Index));
        }

        // Không tin số lượng/tồn kho từ form: kiểm tra lại sản phẩm ngay lúc này.
        var product = await _cart.FindSellableAsync(productId, cancellationToken);

        if (product is null || product.StockQuantity <= 0)
        {
            items.Remove(existing);
            CartSession.SaveItems(HttpContext.Session, items);
            SetMessage("warning", $"\"{existing.Name}\" đã hết hàng nên được xóa khỏi giỏ hàng.");
            return RedirectToAction(nameof(Index));
        }

        var limit = Math.Min(product.StockQuantity, CartService.MaxQuantityPerItem);
        var acceptedQuantity = Math.Min(quantity, limit);

        CartService.Apply(existing, product);
        existing.Quantity = acceptedQuantity;
        CartSession.SaveItems(HttpContext.Session, items);

        if (acceptedQuantity < quantity)
        {
            SetMessage("warning", $"Chỉ còn {limit} sản phẩm \"{product.Name}\", số lượng đã được điều chỉnh.");
        }
        else
        {
            SetMessage("success", "Đã cập nhật giỏ hàng.");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>CUS-14 — xóa hẳn một dòng khỏi giỏ.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int productId)
    {
        var items = CartSession.GetItems(HttpContext.Session);
        var existing = items.FirstOrDefault(item => item.ProductId == productId);

        if (existing is not null)
        {
            items.Remove(existing);
            CartSession.SaveItems(HttpContext.Session, items);
            SetMessage("success", $"Đã xóa \"{existing.Name}\" khỏi giỏ hàng.");
        }

        return RedirectToAction(nameof(Index));
    }

    private void SetMessage(string type, string text)
    {
        TempData["StatusMessageType"] = type;
        TempData["StatusMessage"] = text;
    }

    /// <summary>
    /// Quay lại đúng trang người dùng vừa bấm "thêm vào giỏ". Chỉ chấp nhận URL nội bộ để
    /// không biến nút này thành chỗ chuyển hướng sang site khác (open redirect).
    /// </summary>
    private IActionResult Back(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Index));
}
