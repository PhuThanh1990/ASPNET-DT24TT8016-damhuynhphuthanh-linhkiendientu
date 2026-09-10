using Microsoft.AspNetCore.Mvc;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Base class for every controller of the Admin area. It carries the <c>[Area]</c>
/// attribute and is the single place where authorization is switched on.
/// </summary>
/// <remarks>
/// ADM-03 (protect the area with the <c>Admin</c> role) is BLOCKED by CORE-08/09:
/// the project has no ASP.NET Core Identity yet — <c>ApplicationDbContext</c> derives
/// from <c>DbContext</c> (not <c>IdentityDbContext</c>), there is no Identity package
/// reference and no <c>AspNetUsers</c>/<c>AspNetRoles</c> table in the migrations.
/// Adding a fake or cookie-only authentication scheme here would be worse than nothing,
/// so the area is deliberately left open for now.
///
/// When Identity is in place, ADM-03 is finished by:
///   1. uncommenting the <c>[Authorize]</c> attribute below — it is fully qualified, so no
///      extra <c>using</c> is needed and it compiles against the shared framework as is
///   2. making sure <c>app.UseAuthentication()</c> runs before <c>app.UseAuthorization()</c>
///      in Program.cs
/// Every Admin controller inherits from this type, so no other file has to change.
/// </remarks>
// [Microsoft.AspNetCore.Authorization.Authorize(Roles = AdminArea.AdminRole)]
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
