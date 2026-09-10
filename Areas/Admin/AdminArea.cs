using ElectronicStore.Models;

namespace ElectronicStore.Areas.Admin;

/// <summary>
/// Constants shared by every piece of the Admin area, so the area name is written down
/// exactly once.
/// </summary>
public static class AdminArea
{
    /// <summary>Area name used by routing, <c>[Area]</c> and the layout's tag helpers.</summary>
    public const string Name = "Admin";

    /// <summary>
    /// Role guarding the area. Aliases <see cref="AppRoles.Admin"/> rather than repeating
    /// the literal, so the seeder, the controllers and the views cannot drift apart.
    /// </summary>
    public const string AdminRole = AppRoles.Admin;
}
