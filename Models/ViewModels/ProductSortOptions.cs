namespace ElectronicStore.Models.ViewModels;

/// <summary>
/// CUS-10 — the sort orders offered on the product list. The values travel on the query
/// string (<c>?sort=price-asc</c>), so they are short, lowercase and stable.
/// Anything unknown falls back to <see cref="Newest"/> instead of failing.
/// </summary>
public static class ProductSortOptions
{
    public const string Newest = "newest";

    public const string PriceAsc = "price-asc";

    public const string PriceDesc = "price-desc";

    public const string NameAsc = "name";

    /// <summary>Drives the sort dropdown, so the view and the controller never drift apart.</summary>
    public static readonly (string Value, string Label)[] All =
    [
        (Newest, "Mới nhất"),
        (PriceAsc, "Giá tăng dần"),
        (PriceDesc, "Giá giảm dần"),
        (NameAsc, "Tên A → Z"),
    ];

    public static string Normalize(string? sort)
    {
        var value = sort?.Trim().ToLowerInvariant();

        return All.Any(option => option.Value == value) ? value! : Newest;
    }
}
