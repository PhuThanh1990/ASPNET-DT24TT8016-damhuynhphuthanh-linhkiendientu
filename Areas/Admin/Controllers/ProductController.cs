using ElectronicStore.Areas.Admin.Models;
using ElectronicStore.Data;
using ElectronicStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Product administration: list (ADM-08) and Create (ADM-09).
/// Edit/Delete and image upload belong to later tasks and are not implemented here.
/// </summary>
public class ProductController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;

    public ProductController(ApplicationDbContext db) => _db = db;

    // GET /Admin/Product
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Projected instead of Include(Category).Include(Brand): the list only needs the two
        // names and one image path, so this reads three columns rather than whole entity
        // graphs, still in a single join — and no per-row query for the thumbnail.
        var products = await _db.Products
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProductListItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category.Name,
                BrandName = p.Brand.Name,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                // Primary image when there is one, otherwise the first of the gallery.
                PrimaryImageUrl = p.ProductImages
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.SortOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return View(products);
    }

    // GET /Admin/Product/Create
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new ProductCreateViewModel { IsActive = true };
        await FillDropdownsAsync(model, cancellationToken);

        return View(model);
    }

    // POST /Admin/Product/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCreateViewModel model, CancellationToken cancellationToken)
    {
        model.Slug = NormalizeSlug(model.Slug, nameof(model.Slug), maxLength: 220);

        await ValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            // Dropdowns are not posted back, so they have to be rebuilt before re-rendering.
            await FillDropdownsAsync(model, cancellationToken);
            return View(model);
        }

        var product = new Product
        {
            Name = model.Name.Trim(),
            Slug = model.Slug,
            Sku = model.Sku.Trim(),
            ShortDescription = model.ShortDescription?.Trim(),
            Description = model.Description,
            Specifications = model.Specifications,
            Price = model.Price,
            OldPrice = model.OldPrice,
            StockQuantity = model.StockQuantity,
            CategoryId = model.CategoryId,
            BrandId = model.BrandId,
            IsActive = model.IsActive,
            IsFeatured = model.IsFeatured,
            // Server-owned columns: never taken from the form.
            ViewCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique Slug/Sku index or a check constraint rejected the row.
            ModelState.AddModelError(string.Empty,
                "Không lưu được sản phẩm. Slug hoặc SKU có thể đã tồn tại, vui lòng kiểm tra lại.");
            await FillDropdownsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã tạo sản phẩm \"{product.Name}\".";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Rules the data annotations cannot check: unique slug, unique SKU, and foreign keys
    /// that point at rows which really exist.
    /// </summary>
    private async Task ValidateAsync(ProductCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(model.Slug))
        {
            var slugTaken = await _db.Products
                .AsNoTracking()
                .AnyAsync(p => p.Slug == model.Slug, cancellationToken);

            if (slugTaken)
            {
                ModelState.AddModelError(nameof(model.Slug), "Slug này đã được dùng cho sản phẩm khác.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.Sku))
        {
            var sku = model.Sku.Trim();

            var skuTaken = await _db.Products
                .AsNoTracking()
                .AnyAsync(p => p.Sku == sku, cancellationToken);

            if (skuTaken)
            {
                ModelState.AddModelError(nameof(model.Sku), "SKU này đã được dùng cho sản phẩm khác.");
            }
        }

        if (model.CategoryId > 0)
        {
            var categoryExists = await _db.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Id == model.CategoryId, cancellationToken);

            if (!categoryExists)
            {
                ModelState.AddModelError(nameof(model.CategoryId), "Danh mục không tồn tại.");
            }
        }

        if (model.BrandId > 0)
        {
            var brandExists = await _db.Brands
                .AsNoTracking()
                .AnyAsync(b => b.Id == model.BrandId, cancellationToken);

            if (!brandExists)
            {
                ModelState.AddModelError(nameof(model.BrandId), "Thương hiệu không tồn tại.");
            }
        }

        // Mirrors CK_Products_Price / CK_Products_StockQuantity so the user gets a readable
        // message instead of a database error.
        if (model.Price < 0)
        {
            ModelState.AddModelError(nameof(model.Price), "Giá bán không được âm.");
        }

        if (model.OldPrice is < 0)
        {
            ModelState.AddModelError(nameof(model.OldPrice), "Giá gốc không được âm.");
        }

        if (model.StockQuantity < 0)
        {
            ModelState.AddModelError(nameof(model.StockQuantity), "Số lượng tồn kho không được âm.");
        }
    }

    private async Task FillDropdownsAsync(ProductCreateViewModel model, CancellationToken cancellationToken)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.IsActive })
            .ToListAsync(cancellationToken);

        var brands = await _db.Brands
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new { b.Id, b.Name, b.IsActive })
            .ToListAsync(cancellationToken);

        // Hidden rows stay selectable — an admin may prepare a product for a category that is
        // not published yet — but they are labelled so the choice is deliberate.
        model.CategoryOptions = categories
            .Select(c => new SelectListItem(
                c.IsActive ? c.Name : $"{c.Name} (đã ẩn)",
                c.Id.ToString(),
                c.Id == model.CategoryId))
            .ToList();

        model.BrandOptions = brands
            .Select(b => new SelectListItem(
                b.IsActive ? b.Name : $"{b.Name} (đã ẩn)",
                b.Id.ToString(),
                b.Id == model.BrandId))
            .ToList();
    }
}
