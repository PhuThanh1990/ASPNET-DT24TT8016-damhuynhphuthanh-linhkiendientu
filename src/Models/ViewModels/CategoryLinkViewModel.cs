namespace ElectronicStore.Models.ViewModels;

/// <summary>
/// A category as it appears in the navbar menu and in the home page category grid.
/// </summary>
public class CategoryLinkViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    /// <summary>Number of active products in the category; shown as a hint on the card.</summary>
    public int ProductCount { get; set; }

    /// <summary>
    /// Active sub-categories, used by the navbar dropdown. Empty for a flat catalog.
    /// List (not IReadOnlyList) because it is filled inside an EF projection.
    /// </summary>
    public List<CategoryLinkViewModel> Children { get; set; } = [];
}
