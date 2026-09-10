namespace ElectronicStore.Models;

/// <summary>
/// The two Identity roles of the system. Kept as constants so controllers, views and the
/// seeder all refer to the same strings instead of repeating literals.
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";

    public const string Customer = "Customer";

    public static readonly string[] All = [Admin, Customer];
}
