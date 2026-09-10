namespace ElectronicStore.Areas.Admin;

/// <summary>
/// Constants shared by every piece of the Admin area, so the area name and the role
/// name are written down exactly once.
/// </summary>
public static class AdminArea
{
    /// <summary>Area name used by routing, <c>[Area]</c> and the layout's tag helpers.</summary>
    public const string Name = "Admin";

    /// <summary>
    /// Role that guards the whole area, enforced in
    /// <see cref="Controllers.AdminControllerBase"/>. Same value as
    /// <see cref="Models.AppRoles.Admin"/>, which is what the seeder creates.
    /// </summary>
    public const string AdminRole = "Admin";
}
