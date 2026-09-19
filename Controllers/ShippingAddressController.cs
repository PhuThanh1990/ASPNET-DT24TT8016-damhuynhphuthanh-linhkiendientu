using System.Text.RegularExpressions;
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
/// ADDR-01 — sổ địa chỉ giao hàng của khách đang đăng nhập: xem, thêm, sửa, xóa và đặt
/// địa chỉ mặc định.
/// </summary>
/// <remarks>
/// <para>
/// Dữ liệu bên dưới là entity <see cref="Address"/> có sẵn của project — cùng bảng
/// <c>Addresses</c> mà <c>Order.AddressId</c>, <c>OrderService</c> và luồng checkout đang
/// dùng. Cố ý không tạo model địa chỉ thứ hai, để địa chỉ khách lưu ở màn hình này dùng
/// được ngay khi ADDR-03 nối vào trang thanh toán.
/// </para>
/// <para>
/// Chống IDOR: không action nào nhận <c>UserId</c> từ URL hay từ form. Id tài khoản lấy từ
/// cookie đăng nhập, và mọi truy vấn đều lọc kèm <c>UserId</c> — sửa id trên thanh địa chỉ
/// chỉ nhận về 404 chứ không chạm được địa chỉ của người khác.
/// </para>
/// <para>
/// FINAL-ADDR: phần Tỉnh/Thành phố và Phường/Xã dùng chung cơ chế ADDR-02 với trang thanh
/// toán — form chỉ gửi lên mã, controller tra tên từ <see cref="IAdministrativeUnitService"/>
/// rồi lưu cả mã lẫn tên. Cấp Quận/Huyện không còn trong luồng nhập mới (cơ cấu hành chính
/// 2 cấp từ 01/07/2025).
/// </para>
/// <para>
/// Quy tắc "mỗi tài khoản nhiều nhất một địa chỉ mặc định" được database ép bằng filtered
/// unique index <c>UX_Addresses_UserId_Default</c>. Vì vậy mọi thao tác đổi cờ mặc định đều
/// chạy trong một transaction và bỏ cờ cũ TRƯỚC khi gắn cờ mới, dùng
/// <c>ExecuteUpdateAsync</c> để thứ tự hai câu lệnh là chắc chắn (để EF tự sắp xếp lệnh
/// trong một lần SaveChanges thì có thể vi phạm index).
/// </para>
/// </remarks>
[Authorize]
public class ShippingAddressController : Controller
{
    /// <summary>
    /// Độ dài cột Address.Province / Address.Ward (xem AddressConfiguration). Chỉ dùng cho
    /// nhánh nhập tay khi dataset địa giới hành chính không nạp được; nhánh chọn từ danh
    /// sách lấy tên thẳng từ dataset nên luôn nằm trong giới hạn.
    /// </summary>
    private const int ProvinceNameMaxLength = 100;

    private const int WardNameMaxLength = 100;

    private readonly ApplicationDbContext _db;
    private readonly IAdministrativeUnitService _units;
    private readonly UserManager<ApplicationUser> _userManager;

    public ShippingAddressController(
        ApplicationDbContext db,
        IAdministrativeUnitService units,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _units = units;
        _userManager = userManager;
    }

    // GET /ShippingAddress
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        // FullAddress là property tính toán và đã được Ignore trong cấu hình EF, nên không
        // dịch được sang SQL — lấy entity về rồi mới dựng view model ở bộ nhớ. Sổ địa chỉ
        // của một tài khoản rất nhỏ nên không có vấn đề gì về hiệu năng.
        var addresses = await OwnedBy(userId)
            .AsNoTracking()
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        var model = new AddressListViewModel
        {
            Addresses = addresses.Select(ToListItem).ToList()
        };

        return View(model);
    }

    // GET /ShippingAddress/Create
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var isFirst = !await OwnedBy(userId).AnyAsync(cancellationToken);

        // Địa chỉ đầu tiên bắt buộc là mặc định nên tick sẵn và khóa ô lại.
        var model = new AddressFormViewModel { IsFirstAddress = isFirst, IsDefault = isFirst };
        FillSelectors(model);

        return View(model);
    }

    // POST /ShippingAddress/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AddressFormViewModel model, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var isFirst = !await OwnedBy(userId).AnyAsync(cancellationToken);
        model.IsFirstAddress = isFirst;

        // Phải chạy TRƯỚC khi xét ModelState: hàm này vừa gỡ vừa thêm lỗi tùy theo dataset
        // có nạp được hay không.
        ResolveAdministrativeUnits(model);

        if (!ModelState.IsValid)
        {
            FillSelectors(model);
            return View(model);
        }

        // Cờ mặc định do server quyết định: địa chỉ đầu tiên luôn là mặc định, kể cả khi
        // form gửi lên false.
        var makeDefault = isFirst || model.IsDefault;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (makeDefault)
        {
            await ClearDefaultAsync(userId, cancellationToken);
        }

        _db.Addresses.Add(new Address
        {
            UserId = userId,
            FullName = model.FullName.Trim(),
            PhoneNumber = NormalizePhone(model.PhoneNumber),
            Province = model.Province,
            ProvinceCode = model.ProvinceCode,
            Ward = model.Ward,
            WardCode = model.WardCode,

            // Địa chỉ mới luôn theo cơ cấu 2 cấp nên không có quận/huyện. Cột nullable nên
            // để null thay vì chuỗi rỗng, và FullAddress tự bỏ qua phần này.
            District = null,
            AddressLine = model.AddressLine.Trim(),
            IsDefault = makeDefault,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        SetMessage("success", "Đã thêm địa chỉ mới vào sổ địa chỉ.");
        return RedirectToAction(nameof(Index));
    }

    // GET /ShippingAddress/Edit/5
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var address = await OwnedBy(userId).AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (address is null)
        {
            return NotFound();
        }

        var model = new AddressFormViewModel
        {
            Id = address.Id,
            FullName = address.FullName,
            PhoneNumber = address.PhoneNumber,
            Province = address.Province,
            ProvinceCode = address.ProvinceCode,
            Ward = address.Ward,
            WardCode = address.WardCode,
            District = address.District,
            AddressLine = address.AddressLine,
            IsDefault = address.IsDefault,
            IsCurrentDefault = address.IsDefault,
            LegacyLocation = DescribeLegacyLocation(address)
        };

        // FillSelectors nạp phường/xã theo ProvinceCode đang có, nên select thứ hai đã sẵn
        // đúng lựa chọn cũ ngay lần render đầu, không phải chờ JavaScript gọi API.
        FillSelectors(model);

        return View(model);
    }

    // POST /ShippingAddress/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        [FromRoute] int id,
        AddressFormViewModel model,
        CancellationToken cancellationToken)
    {
        // [FromRoute] là cố ý: value provider đọc form trước route, nên "int id" trần sẽ
        // nhận giá trị từ trường Id trong form và phép so sánh dưới đây thành vô nghĩa.
        if (id != model.Id)
        {
            return BadRequest();
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var address = await OwnedBy(userId).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (address is null)
        {
            return NotFound();
        }

        model.IsCurrentDefault = address.IsDefault;
        model.LegacyLocation = DescribeLegacyLocation(address);

        ResolveAdministrativeUnits(model);

        if (!ModelState.IsValid)
        {
            FillSelectors(model);
            return View(model);
        }

        // Đang là mặc định thì giữ nguyên: bỏ cờ ở đây sẽ khiến tài khoản không còn địa chỉ
        // mặc định nào. Muốn chuyển thì đặt địa chỉ khác làm mặc định ở trang danh sách.
        var makeDefault = address.IsDefault || model.IsDefault;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (makeDefault && !address.IsDefault)
        {
            await ClearDefaultAsync(userId, cancellationToken);
        }

        address.FullName = model.FullName.Trim();
        address.PhoneNumber = NormalizePhone(model.PhoneNumber);
        address.Province = model.Province;
        address.ProvinceCode = model.ProvinceCode;
        address.Ward = model.Ward;
        address.WardCode = model.WardCode;

        // Chọn lại được tỉnh/phường nghĩa là địa chỉ đã chuyển sang cơ cấu 2 cấp: quận/huyện
        // cũ không còn ứng với phường/xã vừa chọn nên phải bỏ, nếu giữ thì FullAddress sẽ
        // ghép ra một địa chỉ không có thật. Ở nhánh nhập tay (dataset hỏng) thì không đụng
        // tới cột này vì người dùng chưa chọn lại gì.
        if (_units.IsAvailable)
        {
            address.District = null;
        }

        address.AddressLine = model.AddressLine.Trim();
        address.IsDefault = makeDefault;
        address.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        SetMessage("success", "Đã cập nhật địa chỉ.");
        return RedirectToAction(nameof(Index));
    }

    // GET /ShippingAddress/Delete/5 — trang xác nhận
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var address = await OwnedBy(userId).AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (address is null)
        {
            return NotFound();
        }

        return View(ToListItem(address));
    }

    // POST /ShippingAddress/Delete/5
    [HttpPost, ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed([FromRoute] int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var address = await OwnedBy(userId).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (address is null)
        {
            return NotFound();
        }

        var wasDefault = address.IsDefault;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        _db.Addresses.Remove(address);
        await _db.SaveChangesAsync(cancellationToken);

        var promoted = false;

        if (wasDefault)
        {
            // Xóa mất địa chỉ mặc định thì đôn địa chỉ mới nhất còn lại lên thay, để tài
            // khoản không rơi vào trạng thái có địa chỉ nhưng không có cái nào mặc định.
            var replacementId = await OwnedBy(userId)
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (replacementId is { } newDefaultId)
            {
                await _db.Addresses
                    .Where(a => a.Id == newDefaultId && a.UserId == userId)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(a => a.IsDefault, true)
                            .SetProperty(a => a.UpdatedAt, DateTime.UtcNow),
                        cancellationToken);

                promoted = true;
            }
        }

        await transaction.CommitAsync(cancellationToken);

        SetMessage("success", promoted
            ? "Đã xóa địa chỉ. Một địa chỉ khác đã được đặt làm mặc định."
            : "Đã xóa địa chỉ.");

        return RedirectToAction(nameof(Index));
    }

    // POST /ShippingAddress/SetDefault/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault([FromRoute] int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        // Lọc kèm UserId: id của người khác không tìm thấy nên không đổi được gì.
        var exists = await OwnedBy(userId).AnyAsync(a => a.Id == id, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        await ClearDefaultAsync(userId, cancellationToken);

        await _db.Addresses
            .Where(a => a.Id == id && a.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(a => a.IsDefault, true)
                    .SetProperty(a => a.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        SetMessage("success", "Đã đặt địa chỉ mặc định.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// FINAL-ADDR — đổ dữ liệu cho hai selectbox Tỉnh/Thành phố và Phường/Xã.
    /// </summary>
    /// <remarks>
    /// Phường/xã render sẵn theo tỉnh đang chọn để khi mở lại form (sửa địa chỉ cũ hoặc vừa
    /// lỗi validate) lựa chọn cũ vẫn còn dù JavaScript chưa chạy. Giống hệt
    /// <c>CheckoutController.Fill</c> nên hai màn hình luôn hiển thị cùng một danh sách.
    /// </remarks>
    private void FillSelectors(AddressFormViewModel model)
    {
        model.SelectorsAvailable = _units.IsAvailable;
        model.Provinces = _units.GetProvinces();
        model.Wards = _units.GetWards(model.ProvinceCode);
    }

    /// <summary>
    /// FINAL-ADDR — đổi <c>ProvinceCode</c>/<c>WardCode</c> thành tên và kiểm tra hợp lệ.
    /// </summary>
    /// <remarks>
    /// Tên tỉnh/phường LUÔN lấy từ dataset chứ không lấy từ form: người dùng có thể sửa
    /// <c>&lt;option&gt;</c> hay thêm <c>&lt;input name="Province"&gt;</c> trong DevTools,
    /// nhưng tên ghi vào database vẫn là tên thật ứng với mã. Hàm cũng kiểm tra phường/xã có
    /// đúng thuộc tỉnh đã chọn hay không, vì ghép hai mã có thật của hai tỉnh khác nhau sẽ
    /// ra một địa chỉ không tồn tại.
    ///
    /// Khi dataset không nạp được thì form đã đổi sang ô nhập chữ, nên ở đây bỏ ràng buộc
    /// theo mã và quay lại kiểm tra hai ô tên — mất file JSON không được làm website mất
    /// luôn khả năng thêm địa chỉ.
    /// </remarks>
    private void ResolveAdministrativeUnits(AddressFormViewModel model)
    {
        if (!_units.IsAvailable)
        {
            ModelState.Remove(nameof(model.ProvinceCode));
            ModelState.Remove(nameof(model.WardCode));
            model.ProvinceCode = string.Empty;
            model.WardCode = string.Empty;

            // Ô để trống được model binder đổi thành null (ConvertEmptyStringToNull), nên
            // .Trim() phải là ?. — gọi thẳng là NullReferenceException đúng vào trường hợp
            // người dùng bỏ trống hai ô này. Và vì đây là string không nullable, MVC cũng đã
            // tự thêm lỗi required mặc định bằng tiếng Anh; gỡ hai entry đó rồi tự kiểm tra
            // để thông báo thống nhất tiếng Việt, kèm luôn giới hạn độ dài vì [StringLength]
            // nằm trong chính entry vừa gỡ.
            model.Province = model.Province?.Trim() ?? string.Empty;
            model.Ward = model.Ward?.Trim() ?? string.Empty;

            ModelState.Remove(nameof(model.Province));
            ModelState.Remove(nameof(model.Ward));

            if (model.Province.Length == 0)
            {
                ModelState.AddModelError(nameof(model.Province), "Vui lòng nhập Tỉnh/Thành phố.");
            }
            else if (model.Province.Length > ProvinceNameMaxLength)
            {
                ModelState.AddModelError(nameof(model.Province),
                    $"Tỉnh/Thành phố tối đa {ProvinceNameMaxLength} ký tự.");
            }

            if (model.Ward.Length == 0)
            {
                ModelState.AddModelError(nameof(model.Ward), "Vui lòng nhập Phường/Xã.");
            }
            else if (model.Ward.Length > WardNameMaxLength)
            {
                ModelState.AddModelError(nameof(model.Ward),
                    $"Phường/Xã tối đa {WardNameMaxLength} ký tự.");
            }

            return;
        }

        // Hai ô này không có trong form ở chế độ selectbox. Client vẫn post kèm được, nên gỡ
        // cả giá trị lẫn lỗi validate của chúng trước khi tự tra tên — không thì một
        // "Province" dài 500 ký tự do client bịa ra sẽ chặn form vì một field vô hình.
        ModelState.Remove(nameof(model.Province));
        ModelState.Remove(nameof(model.Ward));
        model.Province = string.Empty;
        model.Ward = string.Empty;

        var province = _units.FindProvince(model.ProvinceCode);
        if (province is null)
        {
            // Bỏ trống thì [Required] đã báo rồi, ở đây chỉ báo trường hợp gửi mã lạ.
            if (!string.IsNullOrWhiteSpace(model.ProvinceCode))
            {
                ModelState.AddModelError(nameof(model.ProvinceCode), "Tỉnh/Thành phố không hợp lệ.");
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
                    "Phường/Xã không hợp lệ hoặc không thuộc Tỉnh/Thành phố đã chọn.");
            }

            return;
        }

        model.Ward = ward.Name;
    }

    /// <summary>
    /// Địa chỉ ghi trước ADDR-02 chưa có mã tỉnh/phường: trả về phần địa giới cũ dưới dạng
    /// một dòng chữ để form hiện lại cho người dùng đối chiếu trước khi chọn lại. Địa chỉ đã
    /// có mã thì trả về chuỗi rỗng.
    /// </summary>
    private static string DescribeLegacyLocation(Address address)
    {
        if (!string.IsNullOrWhiteSpace(address.ProvinceCode) && !string.IsNullOrWhiteSpace(address.WardCode))
        {
            return string.Empty;
        }

        return string.Join(", ", new[] { address.Ward, address.District, address.Province }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static AddressListItemViewModel ToListItem(Address address) => new()
    {
        Id = address.Id,
        FullName = address.FullName,
        PhoneNumber = address.PhoneNumber,
        FullAddress = address.FullAddress,
        IsDefault = address.IsDefault,
        CreatedAt = address.CreatedAt
    };

    /// <summary>Chỉ những địa chỉ thuộc về <paramref name="userId"/>. Điểm chặn IDOR duy nhất.</summary>
    private IQueryable<Address> OwnedBy(string userId) =>
        _db.Addresses.Where(a => a.UserId == userId);

    /// <summary>Bỏ cờ mặc định ở mọi địa chỉ hiện có của tài khoản.</summary>
    private Task ClearDefaultAsync(string userId, CancellationToken cancellationToken) =>
        _db.Addresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(a => a.IsDefault, false)
                    .SetProperty(a => a.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

    /// <summary>Bỏ khoảng trắng, dấu chấm và gạch ngang để trong database chỉ còn chữ số.</summary>
    private static string NormalizePhone(string phone) =>
        Regex.Replace(phone.Trim(), @"[\s.\-]", string.Empty);

    private void SetMessage(string type, string text)
    {
        TempData["StatusMessageType"] = type;
        TempData["StatusMessage"] = text;
    }
}
