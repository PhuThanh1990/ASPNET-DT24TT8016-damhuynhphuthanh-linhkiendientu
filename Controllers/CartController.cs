using ElectronicStore.Data;
using ElectronicStore.Helpers;
using ElectronicStore.Models;
using ElectronicStore.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Controllers;

/// <summary>
/// CUS-12 → CUS-15 — giỏ hàng lưu trong Session.
///
/// Nguyên tắc bảo mật của controller này: client chỉ được gửi lên <c>productId</c> và
/// <c>quantity</c>. Tên, ảnh, giá và tồn kho luôn được đọc lại từ database — không bao giờ
/// lấy từ form hay từ Session. Nhờ vậy sửa hidden input trên trình duyệt không đổi được giá.
///
/// Chưa tạo Order ở đây: Checkout là CUS-16 và OrderService do bạn Tài phụ trách.
/// </summary>
public class CartController : Controller
{
    /// <summary>Trần số lượng cho mỗi dòng, để một sản phẩm không nuốt hết tồn kho.</summary>
    private const int MaxQuantityPerItem = 99;

    private readonly ApplicationDbContext _context;

    public CartController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>CUS-13 — trang giỏ hàng. Mỗi lần mở đều đối chiếu lại với database.</summary>
    public async Task<IActionResult> Index()
    {
        return View(await BuildCartAsync());
    }

    /// <summary>CUS-12 — thêm sản phẩm vào giỏ.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity = 1, string? returnUrl = null)
    {
        // Số lượng gửi lên có thể là 0, số âm hoặc rất lớn -> kéo về khoảng hợp lệ.
        quantity = Math.Clamp(quantity, 1, MaxQuantityPerItem);

        var product = await ToSnapshots(SellableProducts().Where(p => p.Id == productId))
            .FirstOrDefaultAsync();

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

        var limit = Math.Min(product.StockQuantity, MaxQuantityPerItem);
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
            items.Add(CreateItem(product, acceptedQuantity));
        }
        else
        {
            // Đã có trong giỏ thì cộng dồn, đồng thời làm mới thông tin theo database.
            Refresh(existing, product);
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
    public async Task<IActionResult> Update(int productId, int quantity, int? delta = null)
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
            quantity = existing.Quantity + Math.Clamp(delta.Value, -MaxQuantityPerItem, MaxQuantityPerItem);
        }

        if (quantity <= 0)
        {
            items.Remove(existing);
            CartSession.SaveItems(HttpContext.Session, items);
            SetMessage("success", $"Đã xóa \"{existing.Name}\" khỏi giỏ hàng.");
            return RedirectToAction(nameof(Index));
        }

        // Không tin số lượng/tồn kho từ form: kiểm tra lại sản phẩm ngay lúc này.
        var product = await ToSnapshots(SellableProducts().Where(p => p.Id == productId))
            .FirstOrDefaultAsync();

        if (product is null || product.StockQuantity <= 0)
        {
            items.Remove(existing);
            CartSession.SaveItems(HttpContext.Session, items);
            SetMessage("warning", $"\"{existing.Name}\" đã hết hàng nên được xóa khỏi giỏ hàng.");
            return RedirectToAction(nameof(Index));
        }

        var limit = Math.Min(product.StockQuantity, MaxQuantityPerItem);
        var acceptedQuantity = Math.Min(quantity, limit);

        Refresh(existing, product);
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

    /// <summary>
    /// Dựng giỏ hàng để hiển thị, đồng thời tự sửa những dòng không còn hợp lệ: sản phẩm
    /// bị gỡ bán, hết hàng, số lượng vượt tồn kho hoặc giá đã đổi. Giỏ hàng trong Session
    /// được ghi lại nếu có thay đổi, nên trang luôn hiển thị đúng những gì đặt được.
    /// </summary>
    private async Task<CartViewModel> BuildCartAsync()
    {
        var items = CartSession.GetItems(HttpContext.Session);
        var model = new CartViewModel();

        if (items.Count == 0)
        {
            return model;
        }

        var productIds = items.Select(item => item.ProductId).ToList();
        var products = await ToSnapshots(SellableProducts().Where(p => productIds.Contains(p.Id)))
            .ToDictionaryAsync(p => p.Id);

        var changed = false;

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || product.StockQuantity <= 0)
            {
                model.Notices.Add($"\"{item.Name}\" không còn bán nên đã được bỏ khỏi giỏ hàng.");
                changed = true;
                continue;
            }

            var limit = Math.Min(product.StockQuantity, MaxQuantityPerItem);
            var quantity = Math.Clamp(item.Quantity, 1, limit);

            if (quantity != item.Quantity)
            {
                model.Notices.Add($"\"{product.Name}\" chỉ còn {limit} sản phẩm, số lượng đã được điều chỉnh.");
                changed = true;
            }

            if (item.UnitPrice != product.Price)
            {
                model.Notices.Add($"Giá của \"{product.Name}\" đã thay đổi thành {product.Price.ToVnd()}.");
                changed = true;
            }

            Refresh(item, product);
            item.Quantity = quantity;
            model.Items.Add(item);
        }

        if (changed)
        {
            CartSession.SaveItems(HttpContext.Session, model.Items);
        }

        return model;
    }

    /// <summary>
    /// Sản phẩm đang được bán. Trả về entity chưa chiếu, để mọi bộ lọc (theo Id, theo danh
    /// sách Id) còn chạy được dưới SQL — xem <see cref="ToSnapshots"/>.
    /// </summary>
    private IQueryable<Product> SellableProducts() =>
        _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

    /// <summary>
    /// Ảnh chụp sản phẩm lấy thẳng từ database — nguồn sự thật duy nhất cho tên, ảnh,
    /// giá và tồn kho.
    /// </summary>
    /// <remarks>
    /// Luôn gọi SAU khi đã lọc xong. Nếu lọc sau bước Select thì EF phải dịch điều kiện
    /// trên chính ProductSnapshot — mà bên trong nó có subquery lấy ảnh — nên không dịch
    /// được và ném InvalidOperationException ngay lúc chạy.
    /// </remarks>
    private static IQueryable<ProductSnapshot> ToSnapshots(IQueryable<Product> products) =>
        products.Select(p => new ProductSnapshot(
            p.Id,
            p.Name,
            p.Slug,
            p.ProductImages
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .ThenBy(i => i.Id)
                .Select(i => i.ImageUrl)
                .FirstOrDefault(),
            p.Price,
            p.StockQuantity));

    private static CartItemViewModel CreateItem(ProductSnapshot product, int quantity)
    {
        var item = new CartItemViewModel { ProductId = product.Id, Quantity = quantity };
        Refresh(item, product);

        return item;
    }

    /// <summary>Đồng bộ dòng giỏ hàng theo dữ liệu database mới nhất (trừ số lượng).</summary>
    private static void Refresh(CartItemViewModel item, ProductSnapshot product)
    {
        item.Name = product.Name;
        item.Slug = product.Slug;
        item.ImageUrl = product.ImageUrl;
        item.UnitPrice = product.Price;
        item.StockQuantity = product.StockQuantity;
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

    private sealed record ProductSnapshot(
        int Id,
        string Name,
        string Slug,
        string? ImageUrl,
        decimal Price,
        int StockQuantity);
}
