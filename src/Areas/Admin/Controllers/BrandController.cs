using ElectronicStore.Areas.Admin.Models;
using ElectronicStore.Data;
using ElectronicStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Brand administration: list (ADM-06) and Create/Edit/Delete (ADM-07).
/// </summary>
public class BrandController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;

    public BrandController(ApplicationDbContext db) => _db = db;

    // GET /Admin/Brand
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var brands = await _db.Brands
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new BrandListItemViewModel
            {
                Id = b.Id,
                Name = b.Name,
                Slug = b.Slug,
                IsActive = b.IsActive,
                CreatedAt = b.CreatedAt,
                ProductCount = b.Products.Count()
            })
            .ToListAsync(cancellationToken);

        return View(brands);
    }

    // GET /Admin/Brand/Create
    public IActionResult Create() => View(new BrandFormViewModel { IsActive = true });

    // POST /Admin/Brand/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BrandFormViewModel model, CancellationToken cancellationToken)
    {
        model.Slug = NormalizeSlug(model.Slug, nameof(model.Slug), maxLength: 120);

        await ValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var brand = new Brand
        {
            Name = model.Name.Trim(),
            Slug = model.Slug,
            Description = model.Description?.Trim(),
            LogoUrl = model.LogoUrl?.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Brands.Add(brand);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Safety net for the unique Slug/Name indexes under concurrent submits.
            ModelState.AddModelError(string.Empty,
                "Không lưu được thương hiệu. Tên hoặc slug có thể đã tồn tại, vui lòng thử lại.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã tạo thương hiệu \"{brand.Name}\".";
        return RedirectToAction(nameof(Index), new { area = AdminArea.Name });
    }

    // GET /Admin/Brand/Edit/5
    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var brand = await _db.Brands
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (brand is null)
        {
            return NotFound();
        }

        return View(new BrandFormViewModel
        {
            Id = brand.Id,
            Name = brand.Name,
            Slug = brand.Slug,
            Description = brand.Description,
            LogoUrl = brand.LogoUrl,
            IsActive = brand.IsActive
        });
    }

    // POST /Admin/Brand/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        [FromRoute] int id,
        BrandFormViewModel model,
        CancellationToken cancellationToken)
    {
        // See CategoryController.Edit: the id must come from the route, otherwise the form
        // would be compared against itself and the guard would never fire.
        if (id != model.Id)
        {
            return BadRequest();
        }

        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (brand is null)
        {
            return NotFound();
        }

        model.Slug = NormalizeSlug(model.Slug, nameof(model.Slug), maxLength: 120);

        await ValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Field-by-field copy: CreatedAt and Products never come from the request.
        brand.Name = model.Name.Trim();
        brand.Slug = model.Slug;
        brand.Description = model.Description?.Trim();
        brand.LogoUrl = model.LogoUrl?.Trim();
        brand.IsActive = model.IsActive;
        brand.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty,
                "Không lưu được thương hiệu. Tên hoặc slug có thể đã tồn tại, vui lòng thử lại.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã cập nhật thương hiệu \"{brand.Name}\".";
        return RedirectToAction(nameof(Index), new { area = AdminArea.Name });
    }

    // GET /Admin/Brand/Delete/5
    public async Task<IActionResult> Delete(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var model = await _db.Brands
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BrandDeleteViewModel
            {
                Id = b.Id,
                Name = b.Name,
                Slug = b.Slug,
                ProductCount = b.Products.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    // POST /Admin/Brand/Delete/5
    [HttpPost, ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed([FromRoute] int id, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (brand is null)
        {
            return NotFound();
        }

        // Re-checked at POST time: Product.BrandId is Restrict, products must never be
        // cascaded away with their brand.
        var productCount = await _db.Products.CountAsync(p => p.BrandId == id, cancellationToken);

        if (productCount > 0)
        {
            TempData["ErrorMessage"] =
                $"Không thể xóa thương hiệu \"{brand.Name}\": đang có {productCount} sản phẩm. " +
                "Hãy chuyển chúng sang thương hiệu khác, hoặc bỏ chọn \"Đang hoạt động\" để ẩn thương hiệu này.";
            return RedirectToAction(nameof(Index), new { area = AdminArea.Name });
        }

        _db.Brands.Remove(brand);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] =
                $"Không thể xóa thương hiệu \"{brand.Name}\": dữ liệu khác đang tham chiếu tới thương hiệu này.";
            return RedirectToAction(nameof(Index), new { area = AdminArea.Name });
        }

        TempData["SuccessMessage"] = $"Đã xóa thương hiệu \"{brand.Name}\".";
        return RedirectToAction(nameof(Index), new { area = AdminArea.Name });
    }

    /// <summary>
    /// Uniqueness rules the data annotations cannot see. Brands have a unique index on both
    /// Slug and Name, so both are checked.
    /// </summary>
    private async Task ValidateAsync(BrandFormViewModel model, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(model.Slug))
        {
            var slugTaken = await _db.Brands
                .AsNoTracking()
                .AnyAsync(b => b.Slug == model.Slug && b.Id != model.Id, cancellationToken);

            if (slugTaken)
            {
                ModelState.AddModelError(nameof(model.Slug), "Slug này đã được dùng cho thương hiệu khác.");
            }
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return;
        }

        var name = model.Name.Trim();

        var nameTaken = await _db.Brands
            .AsNoTracking()
            .AnyAsync(b => b.Name == name && b.Id != model.Id, cancellationToken);

        if (nameTaken)
        {
            ModelState.AddModelError(nameof(model.Name), "Tên thương hiệu này đã tồn tại.");
        }
    }
}
