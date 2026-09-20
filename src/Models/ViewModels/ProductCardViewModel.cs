using System.Linq.Expressions;

namespace ElectronicStore.Models.ViewModels;

/// <summary>
/// Flat data needed by <c>Views/Shared/_ProductCard.cshtml</c>.
/// Built by projecting <see cref="Product"/> inside the query, so the card never touches
/// a navigation property and never triggers an extra round trip.
/// </summary>
public class ProductCardViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? ShortDescription { get; set; }

    public decimal Price { get; set; }

    /// <summary>Null when the product is not discounted.</summary>
    public decimal? OldPrice { get; set; }

    public int StockQuantity { get; set; }

    public string? CategoryName { get; set; }

    public string? BrandName { get; set; }

    /// <summary>Primary image path, or null when the product has no image at all.</summary>
    public string? ImageUrl { get; set; }

    public string? ImageAltText { get; set; }

    public bool InStock => StockQuantity > 0;

    /// <summary>True only when OldPrice is a real, higher "before" price.</summary>
    public bool HasDiscount => OldPrice.HasValue && OldPrice.Value > Price;

    /// <summary>Rounded percentage shown on the discount badge.</summary>
    public int DiscountPercent =>
        HasDiscount ? (int)Math.Round((OldPrice!.Value - Price) / OldPrice.Value * 100) : 0;

    /// <summary>
    /// Single place where a Product is turned into a card. Used with
    /// <c>.Select(ProductCardViewModel.FromProduct)</c> so every list page runs one SQL
    /// statement: the image lookup below becomes a correlated subquery, not an N+1 loop.
    /// </summary>
    public static Expression<Func<Product, ProductCardViewModel>> FromProduct => p => new ProductCardViewModel
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        ShortDescription = p.ShortDescription,
        Price = p.Price,
        OldPrice = p.OldPrice,
        StockQuantity = p.StockQuantity,
        CategoryName = p.Category.Name,
        BrandName = p.Brand.Name,
        ImageUrl = p.ProductImages
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .Select(i => i.ImageUrl)
            .FirstOrDefault(),
        ImageAltText = p.ProductImages
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .Select(i => i.AltText)
            .FirstOrDefault(),
    };
}
