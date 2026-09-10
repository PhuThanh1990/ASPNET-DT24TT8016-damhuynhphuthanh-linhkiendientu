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
    /// Role that will guard the whole area once ASP.NET Core Identity lands (CORE-08/09).
    /// Declared here already so ADM-03 becomes a one-line change in
    /// <see cref="Controllers.AdminControllerBase"/> instead of an edit in every controller.
    /// </summary>
    public const string AdminRole = "Admin";
}
