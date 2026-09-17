using Microsoft.AspNetCore.Identity;

namespace ElectronicStore.Models;

/// <summary>
/// Application user. Extends <see cref="IdentityUser"/> so ASP.NET Core Identity keeps
/// owning credentials, lockout and security stamps; only project specific fields live here.
/// Mapping lives in <c>Data/Configurations/ApplicationUserConfiguration.cs</c>.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Relative path of the avatar under wwwroot.</summary>
    public string? AvatarUrl { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Soft-delete flag: admin disables an account instead of deleting it.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<Address> Addresses { get; set; } = new List<Address>();

    /// <summary>ADDR-01 — sổ địa chỉ giao hàng của tài khoản.</summary>
    public ICollection<ShippingAddress> ShippingAddresses { get; set; } = new List<ShippingAddress>();

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
