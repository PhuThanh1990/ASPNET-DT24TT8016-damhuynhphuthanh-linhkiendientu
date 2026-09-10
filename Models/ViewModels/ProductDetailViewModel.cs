namespace ElectronicStore.Models.ViewModels;

/// <summary>Data for the product detail page.</summary>
public class ProductDetailViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string? ShortDescription { get; set; }

    /// <summary>Full description; may contain HTML written by the admin.</summary>
    public string? Description { get; set; }

    public decimal Price { get; set; }

    public decimal? OldPrice { get; set; }

    public int StockQuantity { get; set; }

    public string? CategoryName { get; set; }

    public string? CategorySlug { get; set; }

    public string? BrandName { get; set; }

    public int ViewCount { get; set; }

    /// <summary>Gallery in display order: primary image first. Empty when the product has none.</summary>
    public List<ProductImageViewModel> Images { get; set; } = [];

    /// <summary>
    /// Specifications parsed from the product's JSON column. Empty when the column is
    /// null, empty or not a flat JSON object — the page simply hides the spec table then.
    /// </summary>
    public List<SpecificationViewModel> Specifications { get; set; } = [];

    public bool InStock => StockQuantity > 0;

    public bool HasDiscount => OldPrice.HasValue && OldPrice.Value > Price;

    public int DiscountPercent =>
        HasDiscount ? (int)Math.Round((OldPrice!.Value - Price) / OldPrice.Value * 100) : 0;

    /// <summary>Image shown in the big frame, or null when the product has no image.</summary>
    public ProductImageViewModel? PrimaryImage => Images.Count > 0 ? Images[0] : null;
}

/// <summary>One image of the detail page gallery.</summary>
public class ProductImageViewModel
{
    public string ImageUrl { get; set; } = string.Empty;

    public string? AltText { get; set; }
}

/// <summary>One row of the technical specification table.</summary>
public class SpecificationViewModel
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
