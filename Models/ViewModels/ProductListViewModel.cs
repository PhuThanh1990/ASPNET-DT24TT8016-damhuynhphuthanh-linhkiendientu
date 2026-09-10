namespace ElectronicStore.Models.ViewModels;

/// <summary>
/// Data for the product list page.
/// Search / filter / sort / paging (CUS-07+) plug in here: add the criteria as
/// properties, apply them to the query in <c>ProductController.Index</c>, and keep
/// <see cref="Products"/> as the already-projected result.
/// </summary>
public class ProductListViewModel
{
    public IReadOnlyList<ProductCardViewModel> Products { get; set; } = [];

    /// <summary>How many products are shown; kept separate from the list for the header text.</summary>
    public int TotalCount => Products.Count;
}
