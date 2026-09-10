using Microsoft.AspNetCore.Mvc;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Base class for every controller of the Admin area. It carries the <c>[Area]</c>
/// attribute and is the single place where authorization is switched on.
/// </summary>
/// <remarks>
/// ADM-03 was blocked while the project had no Identity, so this attribute shipped commented
/// out. CORE-08/09 delivered Identity (<c>IdentityDbContext</c>, the AspNet* tables and the
/// <c>Admin</c>/<c>Customer</c> roles) and Program.cs runs <c>UseAuthentication()</c> before
/// <c>UseAuthorization()</c>, so the guard is now switched on as that note prescribed.
/// Every Admin controller inherits from this type, so no other file has to change:
/// anonymous visitors are sent to the login page, signed-in customers to
/// <c>/Account/AccessDenied</c>.
/// </remarks>
[Microsoft.AspNetCore.Authorization.Authorize(Roles = AdminArea.AdminRole)]
[Area(AdminArea.Name)]
public abstract class AdminControllerBase : Controller
{
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
