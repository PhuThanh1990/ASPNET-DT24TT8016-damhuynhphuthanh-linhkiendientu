using ElectronicStore.Data;
using ElectronicStore.Models;
using ElectronicStore.Models.ViewModels;
using ElectronicStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    /// <summary>Các ô địa chỉ nhập tay — bỏ qua khi khách chọn địa chỉ có sẵn trong sổ.</summary>
    private static readonly string[] ManualAddressFields =
    [
        nameof(CheckoutViewModel.FullName),
        nameof(CheckoutViewModel.Phone),
        nameof(CheckoutViewModel.AddressLine),
        nameof(CheckoutViewModel.Ward),
        nameof(CheckoutViewModel.District),
        nameof(CheckoutViewModel.Province),
    ];

    private readonly ICartService _cart;
    private readonly IOrderService _orders;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICartService cart,
        IOrderService orders,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<CheckoutController> logger)
    {
        _cart = cart;
        _orders = orders;
        _db = db;
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

        await FillAddressBookAsync(model, cancellationToken);

        // Có sổ địa chỉ thì chọn sẵn địa chỉ mặc định — đúng ý nghĩa của cờ mặc định.
        model.SelectedAddressId = model.SavedAddresses.FirstOrDefault(a => a.IsDefault)?.Id;

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

        await FillAddressBookAsync(model, cancellationToken);

        // Chọn địa chỉ trong sổ thì các ô nhập tay bỏ trống là chuyện bình thường: gỡ lỗi
        // validation của riêng những ô đó trước khi xét ModelState.
        if (model.SelectedAddressId.HasValue)
        {
            foreach (var field in ManualAddressFields)
            {
                ModelState.Remove(field);
            }
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

        // Chọn địa chỉ trong sổ thì chỉ gửi đi Id: OrderService tự đọc lại bản ghi, kiểm tra
        // địa chỉ có thuộc tài khoản này không rồi mới chụp tên / số điện thoại / địa chỉ.
        // Nhờ vậy đổi SelectedAddressId trên form cũng không mượn được địa chỉ người khác.
        var usesSavedAddress = model.SelectedAddressId.HasValue;

        var request = new PlaceOrderRequest
        {
            UserId = userId,
            AddressId = model.SelectedAddressId,
            ShippingFullName = usesSavedAddress ? null : model.FullName.Trim(),
            ShippingPhone = usesSavedAddress ? null : model.Phone.Trim(),
            ShippingAddress = usesSavedAddress ? null : model.BuildShippingAddress(),
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
            await FillAddressBookAsync(model, cancellationToken);
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

    /// <summary>
    /// Nạp sổ địa chỉ của khách để trang thanh toán chọn được địa chỉ đã lưu (ADDR-02).
    /// Luôn lọc theo tài khoản đang đăng nhập.
    /// </summary>
    private async Task FillAddressBookAsync(CheckoutViewModel model, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // FullAddress là property tính toán (Ignore trong EF) nên lấy entity về rồi mới map.
        var addresses = await _db.ShippingAddresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        model.SavedAddresses = addresses
            .Select(a => new ShippingAddressListItemViewModel
            {
                Id = a.Id,
                ReceiverName = a.ReceiverName,
                PhoneNumber = a.PhoneNumber,
                FullAddress = a.FullAddress,
                IsDefault = a.IsDefault,
                CreatedAt = a.CreatedAt,
            })
            .ToList();

        // Id không nằm trong sổ của mình (form bị sửa) thì coi như không chọn gì; OrderService
        // vẫn chặn ở tầng dưới, đây chỉ để trang hiển thị đúng thứ khách thực sự có.
        if (model.SelectedAddressId is { } selected &&
            model.SavedAddresses.All(a => a.Id != selected))
        {
            model.SelectedAddressId = null;
        }
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
