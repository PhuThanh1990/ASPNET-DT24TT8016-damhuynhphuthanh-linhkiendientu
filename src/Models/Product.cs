namespace ElectronicStore.Models;

/// <summary>
/// A sellable electronic component.
/// Mapping lives in <c>Data/Configurations/ProductConfiguration.cs</c>.
/// </summary>
public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>URL-friendly unique key, e.g. "ram-corsair-vengeance-16gb".</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Internal stock keeping unit, unique across the catalog.</summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>Teaser shown on product cards.</summary>
    public string? ShortDescription { get; set; }

    /// <summary>Full description, may contain HTML.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Technical specs as a flat JSON object of string key/value pairs, e.g.
    /// <c>{"Socket":"LGA 1700","TDP":"125W"}</c>. Kept as a single column because every
    /// component type has a different set of specs — see docs/database-schema.md § 6.1.
    /// </summary>
    public string? Specifications { get; set; }

    /// <summary>Current selling price. Never negative.</summary>
    public decimal Price { get; set; }

    /// <summary>Price before discount, or null when the product is not discounted.</summary>
    public decimal? OldPrice { get; set; }

    /// <summary>Units on hand. Never negative; decremented at checkout.</summary>
    public int StockQuantity { get; set; }

    public int CategoryId { get; set; }

    public int BrandId { get; set; }

    /// <summary>Soft-delete flag: false hides the product from the shop.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Highlighted on the home page.</summary>
    public bool IsFeatured { get; set; }

    public int ViewCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;

    public Brand Brand { get; set; } = null!;

    public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    /// <summary>Order lines that reference this product. Read-only in practice: each line
    /// carries its own name/price snapshot, so this is only used for sales statistics.</summary>
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
