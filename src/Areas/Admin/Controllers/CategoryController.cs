using ElectronicStore.Areas.Admin.Models;
using ElectronicStore.Data;
using ElectronicStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Category administration: list (ADM-04) and Create/Edit/Delete (ADM-05).
/// </summary>
public class CategoryController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;

    public CategoryController(ApplicationDbContext db) => _db = db;

    // GET /Admin/Category
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Projected straight into the view model: read-only screen, so no tracking, and the
        // two counts decide whether the Delete button may be enabled.
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryListItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ParentCategoryName = c.ParentCategory != null ? c.ParentCategory.Name : null,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                ProductCount = c.Products.Count(),
                ChildCategoryCount = c.ChildCategories.Count()
            })
            .ToListAsync(cancellationToken);

        return View(categories);
    }

    // GET /Admin/Category/Create
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new CategoryFormViewModel
        {
            IsActive = true,
            ParentCategoryOptions = await BuildParentOptionsAsync(null, null, cancellationToken)
        };

        return View(model);
    }

    // POST /Admin/Category/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        model.Slug = NormalizeSlug(model.Slug, nameof(model.Slug), maxLength: 120);

        await ValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            model.ParentCategoryOptions =
                await BuildParentOptionsAsync(model.ParentCategoryId, null, cancellationToken);
            return View(model);
        }

        var category = new Category
        {
            Name = model.Name.Trim(),
            Slug = model.Slug,
            Description = model.Description?.Trim(),
            ImageUrl = model.ImageUrl?.Trim(),
            ParentCategoryId = model.ParentCategoryId,
            SortOrder = model.SortOrder,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Categories.Add(category);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Safety net for the unique slug index when two admins submit at the same time.
            ModelState.AddModelError(string.Empty, "Không lưu được danh mục. Slug có thể đã tồn tại, vui lòng thử lại.");
            model.ParentCategoryOptions =
                await BuildParentOptionsAsync(model.ParentCategoryId, null, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã tạo danh mục \"{category.Name}\".";
        return RedirectToAction(nameof(Index), new { area = AdminArea.Name });
    }

    // GET /Admin/Category/Edit/5
    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var category = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        var model = new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            ParentCategoryId = category.ParentCategoryId,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            ParentCategoryOptions =
                await BuildParentOptionsAsync(category.ParentCategoryId, category.Id, cancellationToken)
        };

        return View(model);
    }

    // POST /Admin/Category/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        [FromRoute] int id,
        CategoryFormViewModel model,
        CancellationToken cancellationToken)
    {
        // [FromRoute] is not decoration: the default value providers read the form BEFORE the
        // route, so a plain "int id" would be filled from the posted Id field and this guard
        // would compare that field against itself. Pinned to the URL, a hidden field that
        // disagrees with the route really does mean the form was tampered with.
        if (id != model.Id)
        {
            return BadRequest();
        }

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        model.Slug = NormalizeSlug(model.Slug, nameof(model.Slug), maxLength: 120);

        await ValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            model.ParentCategoryOptions =
                await BuildParentOptionsAsync(model.ParentCategoryId, model.Id, cancellationToken);
            return View(model);
        }

        // Copied field by field on purpose: CreatedAt and the navigation collections are
        // never taken from the request.
        category.Name = model.Name.Trim();
        category.Slug = model.Slug;
        category.Description = model.Description?.Trim();
        category.ImageUrl = model.ImageUrl?.Trim();
        category.ParentCategoryId = model.ParentCategoryId;
        category.SortOrder = model.SortOrder;
        category.IsActive = model.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

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
            ModelState.AddModelError(string.Empty, "Không lưu được danh mục. Slug có thể đã tồn tại, vui lòng thử lại.");
            model.ParentCategoryOptions =
                await BuildParentOptionsAsync(model.ParentCategoryId, model.Id, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã cập nhật danh mục \"{category.Name}\".";
        return RedirectToAction(nameof(Index), new { area = AdminArea.Name });
    }

    // GET /Admin/Category/Delete/5
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

    // POST /Admin/Category/Delete/5
    [HttpPost, ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed([FromRoute] int id, CancellationToken cancellationToken)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        // Re-checked here, not only in the GET: the confirmation page may be minutes old and
        // Product.CategoryId is Restrict — deleting must never take products down with it.
        var productCount = await _db.Products.CountAsync(p => p.CategoryId == id, cancellationToken);
        var childCount = await _db.Categories.CountAsync(c => c.ParentCategoryId == id, cancellationToken);

        if (productCount > 0 || childCount > 0)
        {
            TempData["ErrorMessage"] =
                $"Không thể xóa danh mục \"{category.Name}\": đang có {productCount} sản phẩm và {childCount} danh mục con. " +
                "Hãy chuyển chúng sang danh mục khác, hoặc bỏ chọn \"Đang hoạt động\" để ẩn danh mục này.";
            return RedirectToAction(nameof(Index), new { area = AdminArea.Name });
        }

        _db.Categories.Remove(category);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A product/child added in the meantime trips the Restrict foreign key.
            TempData["ErrorMessage"] =
                $"Không thể xóa danh mục \"{category.Name}\": dữ liệu khác đang tham chiếu tới danh mục này.";
            return RedirectToAction(nameof(Index), new { area = AdminArea.Name });
        }

        TempData["SuccessMessage"] = $"Đã xóa danh mục \"{category.Name}\".";
        return RedirectToAction(nameof(Index), new { area = AdminArea.Name });
    }

    /// <summary>
    /// Server-side rules that data annotations cannot express: unique slug and a parent that
    /// really exists without creating a loop.
    /// </summary>
    private async Task ValidateAsync(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(model.Slug))
        {
            var slugTaken = await _db.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Slug == model.Slug && c.Id != model.Id, cancellationToken);

            if (slugTaken)
            {
                ModelState.AddModelError(nameof(model.Slug), "Slug này đã được dùng cho danh mục khác.");
            }
        }

        if (model.ParentCategoryId is not { } parentId)
        {
            return;
        }

        if (parentId == model.Id)
        {
            ModelState.AddModelError(nameof(model.ParentCategoryId), "Danh mục không thể là danh mục cha của chính nó.");
            return;
        }

        var parent = await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id == parentId)
            .Select(c => new { c.Id, c.ParentCategoryId })
            .FirstOrDefaultAsync(cancellationToken);

        if (parent is null)
        {
            ModelState.AddModelError(nameof(model.ParentCategoryId), "Danh mục cha không tồn tại.");
        }
        else if (model.Id != 0 && parent.ParentCategoryId == model.Id)
        {
            // Would produce A -> B -> A. Nesting stays one level deep by design.
            ModelState.AddModelError(nameof(model.ParentCategoryId), "Không thể chọn danh mục con làm danh mục cha.");
        }
    }

    /// <summary>
    /// Options for the "parent category" dropdown. <paramref name="excludeId"/> keeps the
    /// category being edited (and its direct children) out of its own parent list.
    /// </summary>
    private async Task<IEnumerable<SelectListItem>> BuildParentOptionsAsync(
        int? selectedId,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var candidates = await _db.Categories
            .AsNoTracking()
            .Where(c => excludeId == null || (c.Id != excludeId && c.ParentCategoryId != excludeId))
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        return candidates
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == selectedId))
            .ToList();
    }

    private async Task<CategoryDeleteViewModel?> BuildDeleteViewModelAsync(int id, CancellationToken cancellationToken)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDeleteViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ProductCount = c.Products.Count(),
                ChildCategoryCount = c.ChildCategories.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
