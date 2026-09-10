namespace ElectronicStore.Models.ViewModels;

/// <summary>
/// Data for the product list page: the current page of products plus the search, filter,
/// sort and paging state (CUS-08 → CUS-11). Every piece of that state is echoed back so
/// the filter form, the sort dropdown and the pagination links can keep it on the URL.
/// </summary>
public class ProductListViewModel
{
    /// <summary>Products of the current page only — already paged in SQL.</summary>
    public IReadOnlyList<ProductCardViewModel> Products { get; set; } = [];

    // ----- Trạng thái tìm kiếm / lọc / sắp xếp -----

    /// <summary>Từ khóa đã trim; null khi không tìm kiếm.</summary>
    public string? Keyword { get; set; }

    /// <summary>Slug danh mục đang lọc; null khi không lọc.</summary>
    public string? Category { get; set; }

    /// <summary>Slug thương hiệu đang lọc; null khi không lọc.</summary>
    public string? Brand { get; set; }

    public string Sort { get; set; } = ProductSortOptions.Newest;

    // ----- Phân trang -----

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 12;

    /// <summary>Tổng số sản phẩm khớp điều kiện, đếm bằng COUNT(*) trước khi phân trang.</summary>
    public int TotalItems { get; set; }

    public int TotalPages { get; set; }

    // ----- Dữ liệu đổ vào bộ lọc -----

    public IReadOnlyList<FilterOptionViewModel> Categories { get; set; } = [];

    public IReadOnlyList<FilterOptionViewModel> Brands { get; set; } = [];

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>True khi người dùng đang tìm kiếm hoặc lọc — dùng để đổi nội dung empty state.</summary>
    public bool HasFilter =>
        !string.IsNullOrEmpty(Keyword) ||
        !string.IsNullOrEmpty(Category) ||
        !string.IsNullOrEmpty(Brand);

    /// <summary>Tên danh mục đang lọc, lấy từ danh sách đã nạp; null nếu slug không hợp lệ.</summary>
    public string? CategoryName =>
        Categories.FirstOrDefault(option => option.Slug == Category)?.Name;

    /// <summary>Tên thương hiệu đang lọc; null nếu slug không hợp lệ.</summary>
    public string? BrandName =>
        Brands.FirstOrDefault(option => option.Slug == Brand)?.Name;

    /// <summary>Vị trí sản phẩm đầu tiên của trang trong toàn bộ kết quả (1-based).</summary>
    public int FirstItemIndex => TotalItems == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;

    public int LastItemIndex => Math.Min(CurrentPage * PageSize, TotalItems);

    /// <summary>
    /// Các số trang hiển thị quanh trang hiện tại (tối đa 5), để thanh phân trang không
    /// dài vô hạn khi catalog lớn.
    /// </summary>
    public IEnumerable<int> PageNumbers
    {
        get
        {
            const int window = 5;

            if (TotalPages <= 0)
            {
                yield break;
            }

            var first = Math.Max(1, CurrentPage - (window / 2));
            var last = Math.Min(TotalPages, first + window - 1);

            // Khi ở gần cuối, kéo cửa sổ lùi lại để vẫn đủ 5 số nếu có thể.
            first = Math.Max(1, last - window + 1);

            for (var page = first; page <= last; page++)
            {
                yield return page;
            }
        }
    }

    /// <summary>
    /// Query string của trang <paramref name="page"/> với đúng keyword/filter/sort hiện
    /// tại. Dùng cho <c>asp-all-route-data</c> ở link phân trang.
    /// </summary>
    public Dictionary<string, string> RouteValues(int page)
    {
        var values = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(Keyword))
        {
            values["keyword"] = Keyword;
        }

        if (!string.IsNullOrEmpty(Category))
        {
            values["category"] = Category;
        }

        if (!string.IsNullOrEmpty(Brand))
        {
            values["brand"] = Brand;
        }

        // Mặc định là "newest" nên không cần bỏ lên URL, giữ link ngắn gọn.
        if (Sort != ProductSortOptions.Newest)
        {
            values["sort"] = Sort;
        }

        if (page > 1)
        {
            values["page"] = page.ToString();
        }

        return values;
    }
}
