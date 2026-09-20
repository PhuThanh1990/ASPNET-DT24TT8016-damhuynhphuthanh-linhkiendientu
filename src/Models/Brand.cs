namespace ElectronicStore.Models;

/// <summary>
/// Manufacturer of a product (Intel, AMD, ASUS, Corsair, ...).
/// Mapping lives in <c>Data/Configurations/BrandConfiguration.cs</c>.
/// </summary>
public class Brand
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>URL-friendly unique key, e.g. "corsair".</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Relative path of the brand logo under wwwroot.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Soft-delete flag: false hides the brand from the shop.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
