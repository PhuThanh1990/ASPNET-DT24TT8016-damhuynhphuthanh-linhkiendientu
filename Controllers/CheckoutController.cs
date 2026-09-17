using ElectronicStore.Models;
using ElectronicStore.Models.ViewModels;
using ElectronicStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicStore.Controllers;

/// <summary>
/// CUS-16 → CUS-18 — đặt hàng từ giỏ hàng.
/// </summary>
/// <remarks>
/// Controller này KHÔNG chứa luật nghiệp vụ đơn hàng. Kiểm tra tồn kho, tra giá, tính tiền,
/// sinh mã đơn, trừ kho và transaction đều nằm trong Core <see cref="IOrderService"/>
/// (CORE-16 → CORE-18). Ở đây chỉ có: dựng form, validate dữ liệu nhập, gom giỏ hàng thành
/// <see cref="PlaceOrderRequest"/>, và dịch kết quả trả về thành trang web.
///
/// Thanh toán online (VNPay...) không thuộc phạm vi task này.
/// </remarks>
[Authorize]
public class CheckoutController : Controller
{
    private readonly ICartService _cart;
    private readonly IOrderService _orders;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICartService cart,
        IOrderService orders,
        UserManager<ApplicationUser> userManager,
        ILogger<CheckoutController> logger)
    {
        _cart = cart;
        _orders = orders;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>CUS-16 — form thanh toán. Giỏ rỗng thì không có gì để đặt.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var cart = await _cart.BuildAsync(HttpContext.Session, cancellationToken);

        if (cart.IsEmpty)
        {
            return RedirectToEmptyCart(cart);
        }

        var model = new CheckoutViewModel();

        // Điền sẵn theo hồ sơ tài khoản cho đỡ phải gõ lại; khách vẫn sửa được.
        var user = await _userManager.GetUserAsync(User);
        if (user is not null)
        {
            model.FullName = user.FullName;
            model.Phone = user.PhoneNumber ?? string.Empty;
        }

        Fill(model, cart);

        return View(model);
    }

    /// <summary>
    /// CUS-17 — tạo đơn hàng.
    /// Theo mẫu POST → Redirect → GET: đặt hàng xong luôn chuyển hướng sang
    /// <see cref="Success"/>, nên bấm F5 ở trang kết quả chỉ tải lại một trang GET chứ
    /// không gửi lại form. Ngoài ra giỏ hàng đã bị xóa sau khi đặt thành công, nên một
    /// lần POST lặp lại (bấm Back rồi gửi lại) sẽ rơi vào nhánh "giỏ hàng trống" ở dưới
    /// thay vì tạo thêm đơn thứ hai.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutViewModel model, CancellationToken cancellationToken)
    {
        var cart = await _cart.BuildAsync(HttpContext.Session, cancellationToken);

        if (cart.IsEmpty)
        {
            return RedirectToEmptyCart(cart);
        }

        if (!ModelState.IsValid)
        {
            Fill(model, cart);
            return View(model);
        }

        // Kho hoặc giá vừa đổi so với lúc khách xem trang: dựng lại giỏ đã tự điều chỉnh
        // số lượng, nên nếu đặt luôn thì khách sẽ mua ít hơn (hoặc giá khác) mà không hề
        // biết. Hiển thị lại các thay đổi đó và để khách bấm "Đặt hàng" lần nữa để xác nhận.
        if (cart.Notices.Count > 0)
        {
            Fill(model, cart);
            return View(model);
        }

        // UserId lấy từ cookie đăng nhập đã được xác thực, không bao giờ từ form.
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var request = new PlaceOrderRequest
        {
            UserId = userId,
            ShippingFullName = model.FullName.Trim(),
            ShippingPhone = model.Phone.Trim(),
            ShippingAddress = model.BuildShippingAddress(),
            Note = model.Note,

            // Chỉ gửi đi id và số lượng: giá và tên do service tự đọc từ database.
            Items = cart.Items
                .Select(item => new OrderItemRequest(item.ProductId, item.Quantity))
                .ToList(),
        };

        var result = await _orders.PlaceOrderAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            // Thất bại thì giỏ hàng phải còn nguyên để khách sửa lại rồi đặt tiếp.
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            _logger.LogInformation("Checkout của {UserId} không thành công: {Code} - {Errors}",
                userId, result.ErrorCode, string.Join(" | ", result.Errors));

            // Tồn kho có thể vừa đổi, dựng lại giỏ để khách thấy đúng tình trạng hiện tại.
            Fill(model, await _cart.BuildAsync(HttpContext.Session, cancellationToken));
            return View(model);
        }

        // CUS-18 — chỉ xóa giỏ hàng sau khi đơn đã được tạo thành công.
        _cart.Clear(HttpContext.Session);

        return RedirectToAction(nameof(Success), new { id = result.Value!.Id });
    }

    /// <summary>
    /// CUS-18 — trang xác nhận đặt hàng thành công.
    /// Đơn được đọc qua <c>GetForCustomerAsync</c> nên đổi id trên URL sang đơn của người
    /// khác chỉ nhận được 404, không lộ bất kỳ dữ liệu nào.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Success(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var result = await _orders.GetForCustomerAsync(id, userId, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound();
        }

        return View(CustomerOrderDetailViewModel.FromOrder(result.Value!));
    }

    /// <summary>Gắn phần hiển thị (giỏ hàng, phí ship, tổng tiền) vào model.</summary>
    /// <remarks>
    /// Phí vận chuyển hỏi thẳng <see cref="IOrderService.QuoteShippingFee"/> để con số trên
    /// trang thanh toán đúng bằng con số đơn hàng sẽ mang, không tự tính lại công thức.
    /// </remarks>
    private void Fill(CheckoutViewModel model, CartViewModel cart)
    {
        model.Cart = cart;
        model.ShippingFee = _orders.QuoteShippingFee(cart.SubTotal);
    }

    /// <summary>
    /// Giỏ rỗng thì không có gì để đặt. Nếu giỏ vừa bị dọn sạch vì sản phẩm ngừng bán hay
    /// hết hàng thì báo đúng lý do đó, thay vì chỉ nói "giỏ hàng trống".
    /// </summary>
    private IActionResult RedirectToEmptyCart(CartViewModel cart)
    {
        TempData["StatusMessageType"] = "warning";
        TempData["StatusMessage"] = cart.Notices.Count > 0
            ? string.Join(" ", cart.Notices)
            : "Giỏ hàng đang trống nên chưa thể thanh toán.";

        return RedirectToAction(nameof(CartController.Index), "Cart");
    }
}
