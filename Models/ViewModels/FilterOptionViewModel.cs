namespace ElectronicStore.Models.ViewModels;

/// <summary>
/// One entry of the category / brand dropdowns on the product list filter bar.
/// Both filters work on <c>Slug</c> so the URL stays readable: <c>?category=cam-bien</c>.
/// </summary>
public class FilterOptionViewModel
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
}
