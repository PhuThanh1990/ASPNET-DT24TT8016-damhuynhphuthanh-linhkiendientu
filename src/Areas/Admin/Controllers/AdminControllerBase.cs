using ElectronicStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Base class for every controller of the Admin area. It keeps the area name and the role
/// requirement in one place, so an admin screen added later only has to inherit from it and
/// can never be shipped without protection. Anonymous visitors are sent to the login page,
/// signed-in customers get /Account/AccessDenied.
/// </summary>
[Area(AdminArea.Name)]
[Authorize(Roles = AppRoles.Admin)]
public abstract class AdminControllerBase : Controller
{
    // QUY ƯỚC: mọi RedirectToAction trong khu Admin PHẢI truyền area = AdminArea.Name.
    //
    // Program.cs đăng ký một loạt route "đẹp" cho khách hàng (san-pham, don-hang, gio-hang...)
    // TRƯỚC route {area:exists}. Những route đó chỉ khai báo {controller, action}, không có
    // area, nên khi sinh URL, LinkGenerator duyệt theo thứ tự đăng ký và chúng khớp trước —
    // kể cả khi đang đứng trong Admin. Ambient area KHÔNG tự động được coi là ràng buộc.
    //
    // Hậu quả nếu quên: RedirectToAction(nameof(Details), new { id }) ở Admin/Order sinh ra
    // /don-hang/{id} (trang đơn hàng của khách → 404 → trang lỗi storefront), còn
    // RedirectToAction(nameof(Index)) ở Admin/Product sinh ra /san-pham (danh sách sản phẩm
    // của khách). Ghi rõ area là cách duy nhất chặn được; đặt area = "" cho các route khách
    // trong Program.cs đã thử và KHÔNG có tác dụng.

    /// <summary>
    /// Normalizes a slug typed by the admin and refreshes its <see cref="Controller.ModelState"/>
    /// entry.
    /// </summary>
    /// <remarks>
    /// Data annotations run during model binding, i.e. against the raw input. Normalizing
    /// afterwards without touching ModelState would leave a stale error on a value that is
    /// perfectly valid once normalized ("Ổ Cứng SSD" -> "o-cung-ssd"), so the old entry is
    /// dropped and the normalized value is checked instead. The normalizer only ever emits
    /// lowercase letters, digits and single dashes, so length is all that is left to verify.
    /// </remarks>
    /// <param name="rawSlug">Value as posted by the form.</param>
    /// <param name="modelStateKey">Name of the slug property, e.g. <c>nameof(model.Slug)</c>.</param>
    /// <param name="maxLength">Column length of the slug in the database.</param>
    /// <returns>The value to store.</returns>
    protected string NormalizeSlug(string? rawSlug, string modelStateKey, int maxLength)
    {
        var slug = SlugHelper.Normalize(rawSlug);

        ModelState.Remove(modelStateKey);

        if (slug.Length == 0)
        {
            ModelState.AddModelError(modelStateKey,
                "Slug là bắt buộc và phải chứa ít nhất một chữ cái hoặc chữ số.");
        }
        else if (slug.Length > maxLength)
        {
            ModelState.AddModelError(modelStateKey, $"Slug tối đa {maxLength} ký tự.");
        }

        return slug;
    }
}
