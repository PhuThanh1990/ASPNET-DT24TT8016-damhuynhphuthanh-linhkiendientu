using ElectronicStore.Areas.Admin.Models;
using ElectronicStore.Areas.Admin.Services;
using ElectronicStore.Data;
using ElectronicStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Product administration: list (ADM-08), Create (ADM-09), Edit (ADM-10),
/// disable/delete (ADM-11), images (ADM-12) and stock (ADM-13).
/// </summary>
public class ProductController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ProductImageStorage _imageStorage;

    public ProductController(ApplicationDbContext db, ProductImageStorage imageStorage)
    {
        _db = db;
        _imageStorage = imageStorage;
    }

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

        await ValidateAsync(model, currentProductId: 0, cancellationToken);

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

    // GET /Admin/Product/Edit/5
    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var model = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductEditViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Sku = p.Sku,
                ShortDescription = p.ShortDescription,
                Description = p.Description,
                Specifications = p.Specifications,
                Price = p.Price,
                OldPrice = p.OldPrice,
                StockQuantity = p.StockQuantity,
                CategoryId = p.CategoryId,
                BrandId = p.BrandId,
                IsActive = p.IsActive,
                IsFeatured = p.IsFeatured
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        await FillDropdownsAsync(model, cancellationToken);

        return View(model);
    }

    // POST /Admin/Product/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        [FromRoute] int id,
        ProductEditViewModel model,
        CancellationToken cancellationToken)
    {
        // [FromRoute] is not decoration: the default value providers read the form BEFORE the
        // route, so a plain "int id" would be filled from the posted Id field and this guard
        // would compare that field against itself.
        if (id != model.Id)
        {
            return BadRequest();
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        model.Slug = NormalizeSlug(model.Slug, nameof(model.Slug), maxLength: 220);

        await ValidateAsync(model, currentProductId: id, cancellationToken);

        if (!ModelState.IsValid)
        {
            await FillDropdownsAsync(model, cancellationToken);
            return View(model);
        }

        // Copied field by field on purpose: ViewCount, CreatedAt and the navigation
        // collections are never taken from the request.
        product.Name = model.Name.Trim();
        product.Slug = model.Slug;
        product.Sku = model.Sku.Trim();
        product.ShortDescription = model.ShortDescription?.Trim();
        product.Description = model.Description;
        product.Specifications = model.Specifications;
        product.Price = model.Price;
        product.OldPrice = model.OldPrice;
        product.StockQuantity = model.StockQuantity;
        product.CategoryId = model.CategoryId;
        product.BrandId = model.BrandId;
        product.IsActive = model.IsActive;
        product.IsFeatured = model.IsFeatured;
        product.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The row disappeared between the read above and the save.
            return NotFound();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty,
                "Không lưu được sản phẩm. Slug hoặc SKU có thể đã tồn tại, vui lòng kiểm tra lại.");
            await FillDropdownsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã cập nhật sản phẩm \"{product.Name}\".";
        return RedirectToAction(nameof(Index));
    }

    // POST /Admin/Product/Disable/5 — the safe, everyday "remove from shop" (ADM-11).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Disable([FromRoute] int id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, isActive: false, cancellationToken);

    // POST /Admin/Product/Activate/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Activate([FromRoute] int id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, isActive: true, cancellationToken);

    // GET /Admin/Product/Delete/5
    public async Task<IActionResult> Delete(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var model = await BuildDeleteViewModelAsync(id.Value, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    // POST /Admin/Product/Delete/5
    [HttpPost, ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed([FromRoute] int id, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .Include(p => p.ProductImages)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        // Re-checked at POST time, not only when the confirmation page was rendered: an order
        // may have been placed in between. OrderDetail.ProductId is Restrict, so the database
        // would refuse anyway — this turns that into a readable message.
        var orderLineCount = await _db.OrderDetails
            .CountAsync(d => d.ProductId == id, cancellationToken);

        if (orderLineCount > 0)
        {
            TempData["ErrorMessage"] =
                $"Không thể xóa vĩnh viễn \"{product.Name}\": sản phẩm đã xuất hiện trong {orderLineCount} " +
                "dòng đơn hàng. Hãy dùng \"Ngừng bán\" để ẩn khỏi cửa hàng mà vẫn giữ lịch sử đơn.";
            return RedirectToAction(nameof(Index));
        }

        // Snapshot the paths before the rows go away, so the files can be cleaned up after a
        // successful commit.
        var imageUrls = product.ProductImages.Select(i => i.ImageUrl).ToList();

        _db.Products.Remove(product);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] =
                $"Không thể xóa \"{product.Name}\": dữ liệu khác đang tham chiếu tới sản phẩm này.";
            return RedirectToAction(nameof(Index));
        }

        // Files are deleted only once the database is committed; the other order would leave
        // rows pointing at files that no longer exist.
        foreach (var url in imageUrls)
        {
            _imageStorage.Delete(url);
        }

        TempData["SuccessMessage"] = $"Đã xóa vĩnh viễn sản phẩm \"{product.Name}\".";
        return RedirectToAction(nameof(Index));
    }

    // POST /Admin/Product/AdjustStock/5 — quick stock correction from the list (ADM-13).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(
        [FromRoute] int id,
        int newQuantity,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        // Mirrors CK_Products_StockQuantity; the check constraint is the real guarantee, this
        // only turns it into a message instead of a database error.
        if (newQuantity < 0)
        {
            TempData["ErrorMessage"] = "Số lượng tồn kho không được âm.";
            return RedirectToAction(nameof(Index));
        }

        var previous = product.StockQuantity;
        product.StockQuantity = newQuantity;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] =
            $"Đã cập nhật tồn kho \"{product.Name}\": {previous} → {newQuantity}.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Admin/Product/Images/5 (ADM-12)
    public async Task<IActionResult> Images(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var model = await BuildImagesViewModelAsync(id.Value, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    // POST /Admin/Product/Images/5
    [HttpPost, ActionName(nameof(Images))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(
        [FromRoute] int id,
        ProductImagesViewModel model,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        // Extension, content type, size and the leading bytes are all checked before a single
        // byte reaches the disk.
        if (!_imageStorage.Validate(model.File, nameof(model.File), ModelState) || !ModelState.IsValid)
        {
            var reload = await BuildImagesViewModelAsync(id, cancellationToken);
            if (reload is null)
            {
                return NotFound();
            }

            reload.AltText = model.AltText;
            reload.IsPrimary = model.IsPrimary;
            return View(nameof(Images), reload);
        }

        var imageUrl = await _imageStorage.SaveAsync(model.File!, cancellationToken);

        var nextSortOrder = await _db.ProductImages
            .Where(i => i.ProductId == id)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        var hasPrimary = await _db.ProductImages
            .AnyAsync(i => i.ProductId == id && i.IsPrimary, cancellationToken);

        var image = new ProductImage
        {
            ProductId = id,
            ImageUrl = imageUrl,
            AltText = string.IsNullOrWhiteSpace(model.AltText) ? product.Name : model.AltText.Trim(),
            SortOrder = nextSortOrder + 1,
            // Never set here: the filtered unique index allows one primary per product, so the
            // promotion below clears the old one first.
            IsPrimary = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.ProductImages.Add(image);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The row failed, so the freshly written file would be an orphan.
            _imageStorage.Delete(imageUrl);
            TempData["ErrorMessage"] = "Không lưu được ảnh, vui lòng thử lại.";
            return RedirectToAction(nameof(Images), new { id });
        }

        // The first image of a product always becomes the thumbnail, otherwise only when asked.
        if (model.IsPrimary || !hasPrimary)
        {
            await PromoteToPrimaryAsync(id, image.Id, cancellationToken);
        }

        TempData["SuccessMessage"] = "Đã tải ảnh lên.";
        return RedirectToAction(nameof(Images), new { id });
    }

    // POST /Admin/Product/SetPrimaryImage/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(
        [FromRoute] int id,
        int imageId,
        CancellationToken cancellationToken)
    {
        var image = await _db.ProductImages
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == id, cancellationToken);

        if (image is null)
        {
            return NotFound();
        }

        await PromoteToPrimaryAsync(id, imageId, cancellationToken);

        TempData["SuccessMessage"] = "Đã đặt ảnh chính.";
        return RedirectToAction(nameof(Images), new { id });
    }

    // POST /Admin/Product/DeleteImage/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(
        [FromRoute] int id,
        int imageId,
        CancellationToken cancellationToken)
    {
        // Scoped by ProductId as well as Id, so an image cannot be deleted through another
        // product's URL.
        var image = await _db.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == id, cancellationToken);

        if (image is null)
        {
            return NotFound();
        }

        var imageUrl = image.ImageUrl;
        var wasPrimary = image.IsPrimary;

        _db.ProductImages.Remove(image);
        await _db.SaveChangesAsync(cancellationToken);

        // Row first, file second: a missing file is harmless, a row pointing at nothing is not.
        _imageStorage.Delete(imageUrl);

        // Keep a thumbnail available: the next image in order takes over.
        if (wasPrimary)
        {
            var replacement = await _db.ProductImages
                .Where(i => i.ProductId == id)
                .OrderBy(i => i.SortOrder)
                .Select(i => (int?)i.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (replacement is { } replacementId)
            {
                await PromoteToPrimaryAsync(id, replacementId, cancellationToken);
            }
        }

        TempData["SuccessMessage"] = "Đã xóa ảnh.";
        return RedirectToAction(nameof(Images), new { id });
    }

    /// <summary>
    /// Makes one image the product thumbnail.
    /// </summary>
    /// <remarks>
    /// <c>UX_ProductImages_ProductId_Primary</c> is a filtered UNIQUE index, so two rows may
    /// never carry <c>IsPrimary = 1</c> at the same time — not even briefly. EF gives no
    /// ordering guarantee inside one SaveChanges, so the old primary is cleared and saved
    /// first, then the new one is set, both inside a transaction.
    /// </remarks>
    private async Task PromoteToPrimaryAsync(int productId, int imageId, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var currentPrimaries = await _db.ProductImages
            .Where(i => i.ProductId == productId && i.IsPrimary && i.Id != imageId)
            .ToListAsync(cancellationToken);

        if (currentPrimaries.Count > 0)
        {
            foreach (var current in currentPrimaries)
            {
                current.IsPrimary = false;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        var target = await _db.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, cancellationToken);

        if (target is not null && !target.IsPrimary)
        {
            target.IsPrimary = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IActionResult> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        product.IsActive = isActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = isActive
            ? $"Đã mở bán lại \"{product.Name}\"."
            : $"Đã ngừng bán \"{product.Name}\". Sản phẩm bị ẩn khỏi cửa hàng nhưng lịch sử đơn hàng giữ nguyên.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<ProductDeleteViewModel?> BuildDeleteViewModelAsync(
        int id,
        CancellationToken cancellationToken) =>
        await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDeleteViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                IsActive = p.IsActive,
                OrderLineCount = p.OrderDetails.Count(),
                ImageCount = p.ProductImages.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ProductImagesViewModel?> BuildImagesViewModelAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return null;
        }

        var images = await _db.ProductImages
            .AsNoTracking()
            .Where(i => i.ProductId == productId)
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.SortOrder)
            .Select(i => new ProductImageViewModel
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
                AltText = i.AltText,
                IsPrimary = i.IsPrimary,
                SortOrder = i.SortOrder
            })
            .ToListAsync(cancellationToken);

        return new ProductImagesViewModel
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Images = images
        };
    }

    /// <summary>
    /// Rules the data annotations cannot check: unique slug, unique SKU, and foreign keys
    /// that point at rows which really exist.
    /// </summary>
    /// <param name="currentProductId">0 when creating, otherwise the row being edited so it
    /// does not clash with itself.</param>
    private async Task ValidateAsync(
        ProductFormViewModel model,
        int currentProductId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(model.Slug))
        {
            var slugTaken = await _db.Products
                .AsNoTracking()
                .AnyAsync(p => p.Slug == model.Slug && p.Id != currentProductId, cancellationToken);

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
                .AnyAsync(p => p.Sku == sku && p.Id != currentProductId, cancellationToken);

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

    private async Task FillDropdownsAsync(ProductFormViewModel model, CancellationToken cancellationToken)
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
