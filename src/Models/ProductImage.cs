namespace ElectronicStore.Models;

/// <summary>
/// One image of a product gallery. Only the path is stored — image bytes stay on disk
/// under wwwroot. Mapping lives in <c>Data/Configurations/ProductImageConfiguration.cs</c>.
/// </summary>
public class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    /// <summary>Relative path under wwwroot, e.g. "/uploads/products/ram-corsair-1.jpg".</summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Alt attribute for SEO and accessibility.</summary>
    public string? AltText { get; set; }

    /// <summary>Thumbnail used on listing pages. At most one per product.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Gallery ordering; lower values are shown first.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}
