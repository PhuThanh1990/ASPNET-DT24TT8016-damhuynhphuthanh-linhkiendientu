namespace ElectronicStore.Models.ViewModels;

/// <summary>Everything the home page renders, gathered by <c>HomeController.Index</c>.</summary>
public class HomeViewModel
{
    public IReadOnlyList<CategoryLinkViewModel> Categories { get; set; } = [];

    /// <summary>Products flagged IsFeatured; empty when the shop has not flagged any.</summary>
    public IReadOnlyList<ProductCardViewModel> FeaturedProducts { get; set; } = [];

    /// <summary>Newest products by CreatedAt, used as the second product row.</summary>
    public IReadOnlyList<ProductCardViewModel> LatestProducts { get; set; } = [];

    /// <summary>False when the catalog is completely empty, so the view can explain why.</summary>
    public bool HasAnyProduct => FeaturedProducts.Count > 0 || LatestProducts.Count > 0;
}
