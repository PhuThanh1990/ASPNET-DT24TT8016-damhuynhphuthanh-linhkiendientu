using System.Text.RegularExpressions;
using ElectronicStore.Data;
using ElectronicStore.Models;
using ElectronicStore.Models.ViewModels;
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
/// Chống IDOR: không action nào nhận <c>UserId</c> từ URL hay từ form. Id tài khoản lấy từ
/// cookie đăng nhập, và mọi truy vấn đều lọc kèm <c>UserId</c> — sửa id trên thanh địa chỉ
/// chỉ nhận về 404 chứ không chạm được địa chỉ của người khác.
/// </para>
/// <para>
/// Quy tắc "mỗi tài khoản nhiều nhất một địa chỉ mặc định" được database ép bằng filtered
/// unique index. Vì vậy mọi thao tác đổi cờ mặc định đều chạy trong một transaction và bỏ
/// cờ cũ TRƯỚC khi gắn cờ mới, dùng <c>ExecuteUpdateAsync</c> để thứ tự hai câu lệnh là
/// chắc chắn (để EF tự sắp xếp lệnh trong một lần SaveChanges thì có thể vi phạm index).
/// </para>
/// </remarks>
[Authorize]
public class ShippingAddressController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ShippingAddressController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
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

        var model = new ShippingAddressListViewModel
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
        return View(new ShippingAddressFormViewModel { IsFirstAddress = isFirst, IsDefault = isFirst });
    }

    // POST /ShippingAddress/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShippingAddressFormViewModel model, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var isFirst = !await OwnedBy(userId).AnyAsync(cancellationToken);
        model.IsFirstAddress = isFirst;

        if (!ModelState.IsValid)
        {
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

        _db.ShippingAddresses.Add(new ShippingAddress
        {
            UserId = userId,
            ReceiverName = model.ReceiverName.Trim(),
            PhoneNumber = NormalizePhone(model.PhoneNumber),
            ProvinceCode = model.ProvinceCode.Trim(),
            ProvinceName = model.ProvinceName.Trim(),
            DistrictCode = Blank(model.DistrictCode),
            DistrictName = Blank(model.DistrictName),
            WardCode = model.WardCode.Trim(),
            WardName = model.WardName.Trim(),
            StreetAddress = model.StreetAddress.Trim(),
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

        return View(new ShippingAddressFormViewModel
        {
            Id = address.Id,
            ReceiverName = address.ReceiverName,
            PhoneNumber = address.PhoneNumber,
            ProvinceCode = address.ProvinceCode,
            ProvinceName = address.ProvinceName,
            DistrictCode = address.DistrictCode,
            DistrictName = address.DistrictName,
            WardCode = address.WardCode,
            WardName = address.WardName,
            StreetAddress = address.StreetAddress,
            IsDefault = address.IsDefault,
            IsCurrentDefault = address.IsDefault
        });
    }

    // POST /ShippingAddress/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        [FromRoute] int id,
        ShippingAddressFormViewModel model,
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

        if (!ModelState.IsValid)
        {
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

        address.ReceiverName = model.ReceiverName.Trim();
        address.PhoneNumber = NormalizePhone(model.PhoneNumber);
        address.ProvinceCode = model.ProvinceCode.Trim();
        address.ProvinceName = model.ProvinceName.Trim();
        address.DistrictCode = Blank(model.DistrictCode);
        address.DistrictName = Blank(model.DistrictName);
        address.WardCode = model.WardCode.Trim();
        address.WardName = model.WardName.Trim();
        address.StreetAddress = model.StreetAddress.Trim();
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

        _db.ShippingAddresses.Remove(address);
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
                await _db.ShippingAddresses
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

        await _db.ShippingAddresses
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

    private static ShippingAddressListItemViewModel ToListItem(ShippingAddress address) => new()
    {
        Id = address.Id,
        ReceiverName = address.ReceiverName,
        PhoneNumber = address.PhoneNumber,
        FullAddress = address.FullAddress,
        IsDefault = address.IsDefault,
        CreatedAt = address.CreatedAt
    };

    /// <summary>Chỉ những địa chỉ thuộc về <paramref name="userId"/>. Điểm chặn IDOR duy nhất.</summary>
    private IQueryable<ShippingAddress> OwnedBy(string userId) =>
        _db.ShippingAddresses.Where(a => a.UserId == userId);

    /// <summary>Bỏ cờ mặc định ở mọi địa chỉ hiện có của tài khoản.</summary>
    private Task ClearDefaultAsync(string userId, CancellationToken cancellationToken) =>
        _db.ShippingAddresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(a => a.IsDefault, false)
                    .SetProperty(a => a.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

    /// <summary>Bỏ khoảng trắng, dấu chấm và gạch ngang để trong database chỉ còn chữ số.</summary>
    private static string NormalizePhone(string phone) =>
        Regex.Replace(phone.Trim(), @"[\s.\-]", string.Empty);

    /// <summary>Chuỗi rỗng / toàn khoảng trắng lưu thành null cho các cột nullable.</summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void SetMessage(string type, string text)
    {
        TempData["StatusMessageType"] = type;
        TempData["StatusMessage"] = text;
    }
}
