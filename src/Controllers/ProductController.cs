using ElectronicStore.Data;
using ElectronicStore.Helpers;
using ElectronicStore.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Controllers;

/// <summary>
/// Customer-facing catalog pages: the product list and the product detail page.
/// Read-only — nothing here writes to the database.
/// </summary>
public class ProductController : Controller
{
    /// <summary>Số sản phẩm mỗi trang (CUS-11).</summary>
    private const int PageSize = 12;

    private readonly ApplicationDbContext _context;

    public ProductController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Product list with search (CUS-08), category/brand filter (CUS-09), sort (CUS-10)
    /// and server-side paging (CUS-11).
    ///
    /// Thứ tự bắt buộc: IQueryable → search → filter → sort → COUNT → Skip/Take →
    /// projection. Không có <c>ToListAsync</c> nào trước Skip/Take, nên SQL Server chỉ
    /// trả về đúng 12 dòng của trang đang xem.
    /// </summary>
    public async Task<IActionResult> Index(
        string? keyword,
        string? category,
        string? brand,
        string? sort,
        int page = 1)
    {
        keyword = Normalize(keyword);
        category = Normalize(category);
        brand = Normalize(brand);
        sort = ProductSortOptions.Normalize(sort);

        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        // --- CUS-08: search ---
        if (keyword is not null)
        {
            // Contains dịch thành LIKE N'%...%' chạy dưới SQL Server. Collation mặc định
            // của SQL Server không phân biệt hoa thường nên không cần ToLower(), và cũng
            // không nên dùng ToLower() vì nó làm index trên Name mất tác dụng.
            query = query.Where(p =>
                p.Name.Contains(keyword) ||
                p.Sku.Contains(keyword) ||
                p.Brand.Name.Contains(keyword));
        }

        // --- CUS-09: filter theo slug; slug sai chỉ ra 0 kết quả, không lỗi ---
        if (category is not null)
        {
            query = query.Where(p => p.Category.Slug == category);
        }

        if (brand is not null)
        {
            query = query.Where(p => p.Brand.Slug == brand);
        }

        // --- CUS-10: sort ngay trên IQueryable, luôn có Id làm tiêu chí phụ để thứ tự
        // ổn định giữa các trang khi nhiều sản phẩm trùng giá / trùng ngày tạo ---
        query = sort switch
        {
            ProductSortOptions.PriceAsc => query.OrderBy(p => p.Price).ThenByDescending(p => p.Id),
            ProductSortOptions.PriceDesc => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.Id),
            ProductSortOptions.NameAsc => query.OrderBy(p => p.Name).ThenByDescending(p => p.Id),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
        };

        // --- CUS-11: đếm trước, rồi mới cắt trang ---
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)PageSize);

        // page <= 0 hoặc vượt quá số trang đều được kéo về khoảng hợp lệ thay vì lỗi.
        page = Math.Max(page, 1);
        if (totalPages > 0)
        {
            page = Math.Min(page, totalPages);
        }

        var products = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(ProductCardViewModel.FromProduct)
            .ToListAsync();

        var model = new ProductListViewModel
        {
            Products = products,
            Keyword = keyword,
            Category = category,
            Brand = brand,
            Sort = sort,
            CurrentPage = page,
            PageSize = PageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Categories = await LoadCategoryOptionsAsync(),
            Brands = await LoadBrandOptionsAsync(),
        };

        return View(model);
    }

    /// <summary>
    /// Product detail, looked up by the unique slug (see docs/database-schema.md § 6).
    /// Returns 404 for an unknown slug and for a product hidden with IsActive = false.
    /// </summary>
    public async Task<IActionResult> Details(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        // The whole page — product, category, brand and the gallery — is one query.
        // Specifications comes back as raw JSON and is parsed below, outside SQL.
        var row = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Slug == slug)
            .Select(p => new
            {
                Detail = new ProductDetailViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    Sku = p.Sku,
                    ShortDescription = p.ShortDescription,
                    Description = p.Description,
                    Price = p.Price,
                    OldPrice = p.OldPrice,
                    StockQuantity = p.StockQuantity,
                    CategoryName = p.Category.Name,
                    CategorySlug = p.Category.Slug,
                    BrandName = p.Brand.Name,
                    BrandSlug = p.Brand.Slug,
                    ViewCount = p.ViewCount,
                    Images = p.ProductImages
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.SortOrder)
                        .ThenBy(i => i.Id)
                        .Select(i => new ProductImageViewModel
                        {
                            ImageUrl = i.ImageUrl,
                            AltText = i.AltText,
                        })
                        .ToList(),
                },
                SpecificationsJson = p.Specifications,
            })
            .FirstOrDefaultAsync();

        if (row is null)
        {
            return NotFound();
        }

        // CUS-07 — parse ngoài SQL, JSON hỏng chỉ làm bảng thông số trống.
        row.Detail.Specifications = SpecificationParser.Parse(row.SpecificationsJson);

        return View(row.Detail);
    }

    private async Task<IReadOnlyList<FilterOptionViewModel>> LoadCategoryOptionsAsync() =>
        await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new FilterOptionViewModel { Name = c.Name, Slug = c.Slug })
            .ToListAsync();

    private async Task<IReadOnlyList<FilterOptionViewModel>> LoadBrandOptionsAsync() =>
        await _context.Brands
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new FilterOptionViewModel { Name = b.Name, Slug = b.Slug })
            .ToListAsync();

    /// <summary>Trim tham số query string; chuỗi rỗng coi như không truyền.</summary>
    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
