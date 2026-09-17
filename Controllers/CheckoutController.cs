using ElectronicStore.Data;
using ElectronicStore.Models;
using ElectronicStore.Models.ViewModels;
using ElectronicStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    private readonly ICartService _cart;
    private readonly IOrderService _orders;
    private readonly IAdministrativeUnitService _units;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICartService cart,
        IOrderService orders,
        IAdministrativeUnitService units,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<CheckoutController> logger)
    {
        _cart = cart;
        _orders = orders;
        _units = units;
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

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var model = new CheckoutViewModel
        {
            SavedAddresses = await LoadAddressBookAsync(userId, cancellationToken),
        };

        if (model.HasSavedAddresses)
        {
            // ADDR-03 — sổ đã sắp địa chỉ mặc định lên đầu, nên mục đầu tiên chính là mặc
            // định; tài khoản chưa đặt mặc định nào thì đó là địa chỉ mới nhất.
            model.SelectedAddressId = model.SavedAddresses[0].Id;
        }
        else
        {
            // Chưa có địa chỉ nào: mở thẳng form nhập tay và điền sẵn theo hồ sơ tài khoản
            // cho đỡ phải gõ lại; khách vẫn sửa được.
            model.UseNewAddress = true;

            var user = await _userManager.GetUserAsync(User);
            if (user is not null)
            {
                model.FullName = user.FullName;
                model.Phone = user.PhoneNumber ?? string.Empty;
            }
        }

        // Vẫn nạp selectbox tỉnh/phường: khách có thể bấm "Thêm địa chỉ mới" ngay sau đó.
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

        // UserId lấy từ cookie đăng nhập đã được xác thực, không bao giờ từ form.
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        // ADDR-03 — nạp lại sổ địa chỉ của chính tài khoản này rồi chốt xem đơn dùng địa chỉ
        // đã lưu hay địa chỉ nhập tay. Phải chạy TRƯỚC ResolveAdministrativeUnits: hai luồng
        // cần hai bộ kiểm tra khác hẳn nhau.
        model.SavedAddresses = await LoadAddressBookAsync(userId, cancellationToken);
        ValidateAddressChoice(model);

        if (model.ShowNewAddressForm)
        {
            // ADDR-02 — chỉ khi khách thật sự nhập địa chỉ mới mới đổi mã tỉnh/phường thành
            // tên, để lỗi mã sai hiện cùng lượt với các lỗi nhập liệu khác.
            ResolveAdministrativeUnits(model);
        }
        else
        {
            // Dùng địa chỉ trong sổ: mọi ô nhập tay đang bị ẩn và để trống, nên lỗi Required
            // của chúng chỉ làm khách rối. Thông tin giao hàng sẽ do OrderService đọc lại từ
            // database theo AddressId + UserId.
            RemoveNewAddressValidationErrors(model, ModelState);
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

        var request = new PlaceOrderRequest
        {
            UserId = userId,

            // ADDR-03 — chọn từ sổ địa chỉ thì chỉ gửi id: IOrderService tự đọc bản ghi, kiểm
            // tra nó thuộc đúng UserId rồi CHỤP tên/điện thoại/địa chỉ vào đơn. Nhờ vậy khách
            // sửa sổ địa chỉ sau này cũng không làm đổi đơn cũ. Ba field dưới chỉ được service
            // dùng khi AddressId là null (luồng nhập tay).
            AddressId = model.SelectedAddressId,
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
            model.SavedAddresses = await LoadAddressBookAsync(userId, cancellationToken);
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

        // ADDR-02 — đổ dữ liệu cho hai selectbox. Phường/xã render sẵn theo tỉnh đang chọn
        // nên khi mở lại form (lỗi validate) lựa chọn cũ vẫn còn dù JavaScript chưa chạy.
        model.SelectorsAvailable = _units.IsAvailable;
        model.Provinces = _units.GetProvinces();
        model.Wards = _units.GetWards(model.ProvinceCode);
    }

    /// <summary>
    /// ADDR-02 — đổi <c>ProvinceCode</c>/<c>WardCode</c> thành tên và kiểm tra tính hợp lệ.
    /// </summary>
    /// <remarks>
    /// Tên tỉnh/phường LUÔN lấy từ dataset chứ không lấy từ form: người dùng có thể sửa
    /// <c>&lt;option&gt;</c> trong DevTools, nhưng tên ghi vào đơn hàng vẫn là tên thật ứng
    /// với mã. Hàm cũng kiểm tra phường/xã có đúng thuộc tỉnh đã chọn hay không, vì ghép hai
    /// mã có thật của hai tỉnh khác nhau sẽ ra một địa chỉ không tồn tại.
    ///
    /// Khi dataset không nạp được thì form đã đổi sang ô nhập chữ, nên ở đây bỏ ràng buộc
    /// theo mã và quay lại kiểm tra hai ô tên.
    /// </remarks>
    private void ResolveAdministrativeUnits(CheckoutViewModel model)
    {
        if (!_units.IsAvailable)
        {
            ModelState.Remove(nameof(model.ProvinceCode));
            ModelState.Remove(nameof(model.WardCode));
            model.ProvinceCode = string.Empty;
            model.WardCode = string.Empty;
            model.Province = model.Province.Trim();
            model.Ward = model.Ward.Trim();

            if (model.Province.Length == 0)
            {
                ModelState.AddModelError(nameof(model.Province), "Vui lòng nhập tỉnh/thành phố.");
            }

            if (model.Ward.Length == 0)
            {
                ModelState.AddModelError(nameof(model.Ward), "Vui lòng nhập phường/xã.");
            }

            return;
        }

        model.Province = string.Empty;
        model.Ward = string.Empty;

        var province = _units.FindProvince(model.ProvinceCode);
        if (province is null)
        {
            // Bỏ trống thì [Required] đã báo rồi, ở đây chỉ báo trường hợp gửi mã lạ.
            if (!string.IsNullOrWhiteSpace(model.ProvinceCode))
            {
                ModelState.AddModelError(nameof(model.ProvinceCode), "Tỉnh/thành phố không hợp lệ.");
            }

            return;
        }

        model.Province = province.Name;

        var ward = _units.FindWard(model.ProvinceCode, model.WardCode);
        if (ward is null)
        {
            if (!string.IsNullOrWhiteSpace(model.WardCode))
            {
                ModelState.AddModelError(nameof(model.WardCode),
                    "Phường/xã không hợp lệ hoặc không thuộc tỉnh/thành phố đã chọn.");
            }

            return;
        }

        model.Ward = ward.Name;
    }

    /// <summary>
    /// ADDR-03 — sổ địa chỉ của một khách, mặc định lên đầu rồi tới địa chỉ mới nhất.
    /// </summary>
    /// <remarks>
    /// Dùng lại <see cref="AddressListItemViewModel"/> của ADDR-01 nên checkout và trang quản
    /// lý sổ địa chỉ hiển thị cùng một dạng dữ liệu, không có model thứ hai cho cùng khái niệm.
    /// Thứ tự sắp xếp cũng cố ý giống trang sổ địa chỉ để "mục đầu tiên" ở hai nơi là một.
    ///
    /// Luôn lọc theo <paramref name="userId"/>: khách không bao giờ nhìn thấy — và vì vậy
    /// không thể chọn — địa chỉ của người khác. Đây là lớp chặn đầu; OrderService còn kiểm
    /// tra quyền sở hữu một lần nữa khi đặt hàng.
    /// </remarks>
    private async Task<IReadOnlyList<AddressListItemViewModel>> LoadAddressBookAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var addresses = await _db.Addresses
            .AsNoTracking()
            .Where(address => address.UserId == userId)
            .OrderByDescending(address => address.IsDefault)
            .ThenByDescending(address => address.CreatedAt)
            .ThenByDescending(address => address.Id)
            .ToListAsync(cancellationToken);

        // FullAddress là thuộc tính tính toán của entity nên phải ghép sau khi đã nạp về.
        return addresses
            .Select(address => new AddressListItemViewModel
            {
                Id = address.Id,
                FullName = address.FullName,
                PhoneNumber = address.PhoneNumber,
                FullAddress = address.FullAddress,
                IsDefault = address.IsDefault,
                CreatedAt = address.CreatedAt,
            })
            .ToList();
    }

    /// <summary>
    /// ADDR-03 — chốt xem đơn này dùng địa chỉ đã lưu hay địa chỉ nhập tay, và chặn submit
    /// khi khách chưa chọn gì hợp lệ.
    /// </summary>
    private void ValidateAddressChoice(CheckoutViewModel model)
    {
        if (model.UseNewAddress)
        {
            // Khách chủ động nhập địa chỉ mới: bỏ qua mọi lựa chọn trong sổ.
            model.SelectedAddressId = null;
            return;
        }

        if (model.SelectedAddressId is { } selectedId
            && model.SavedAddresses.All(address => address.Id != selectedId))
        {
            // Id không nằm trong sổ của tài khoản này — form bị sửa tay, hoặc địa chỉ vừa bị
            // xóa ở tab khác. Không đoán thay khách: bắt chọn lại.
            model.SelectedAddressId = null;
            ModelState.AddModelError(nameof(model.SelectedAddressId),
                "Địa chỉ giao hàng không hợp lệ, vui lòng chọn lại.");
            return;
        }

        // Có sổ địa chỉ, không bấm "Thêm địa chỉ mới", mà cũng không chọn mục nào.
        if (model.SelectedAddressId is null && model.HasSavedAddresses)
        {
            ModelState.AddModelError(nameof(model.SelectedAddressId),
                "Vui lòng chọn địa chỉ giao hàng.");
        }
    }

    /// <summary>
    /// ADDR-03 — gỡ lỗi validate của form "địa chỉ mới" khi khách đang dùng địa chỉ đã lưu.
    /// </summary>
    /// <remarks>
    /// Các ô này gắn <c>[Required]</c> để phục vụ luồng nhập tay, và attribute đã kịp sinh lỗi
    /// ngay lúc model binding — trước khi controller biết khách chọn luồng nào. Khi hóa ra
    /// khách dùng địa chỉ trong sổ thì các ô đó đang bị ẩn và để trống, nên phải gỡ lỗi đi;
    /// nếu không khách sẽ thấy "Vui lòng chọn tỉnh/thành phố" cho một select họ không nhìn thấy.
    ///
    /// Không có <c>District</c>: ADDR-02 đã bỏ hẳn cấp quận/huyện khỏi form theo cơ cấu hành
    /// chính 2 cấp áp dụng từ 01/07/2025.
    /// </remarks>
    private static void RemoveNewAddressValidationErrors(
        CheckoutViewModel model,
        ModelStateDictionary modelState)
    {
        foreach (var field in new[]
                 {
                     nameof(CheckoutViewModel.FullName),
                     nameof(CheckoutViewModel.Phone),
                     nameof(CheckoutViewModel.AddressLine),
                     nameof(CheckoutViewModel.ProvinceCode),
                     nameof(CheckoutViewModel.WardCode),
                     nameof(CheckoutViewModel.Province),
                     nameof(CheckoutViewModel.Ward),
                 })
        {
            modelState.Remove(field);
        }

        // IValidatableObject.Validate của CheckoutViewModel gắn lỗi "địa chỉ quá dài" vào
        // AddressLine; dòng trên đã gỡ, nhưng lỗi cấp model (key rỗng) thì không đụng tới vì
        // đó là nơi OrderService báo lỗi nghiệp vụ.
        _ = model;
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
