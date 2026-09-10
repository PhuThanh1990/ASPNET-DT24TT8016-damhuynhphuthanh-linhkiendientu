using ElectronicStore.Data;
using ElectronicStore.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.ViewComponents;

/// <summary>
/// Fills the "Danh mục" dropdown in the navbar. Lives in a view component so the layout
/// can show real categories without every controller having to load them.
/// </summary>
public class CategoryMenuViewComponent : ViewComponent
{
    /// <summary>Keeps the dropdown a menu, not a sitemap.</summary>
    private const int MaxRootCategories = 12;

    private readonly ApplicationDbContext _context;

    public CategoryMenuViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        // Root categories plus their active children, in one query. A flat catalog
        // (no parent/child nesting) simply comes back with empty Children lists.
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.ParentCategoryId == null)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Take(MaxRootCategories)
            .Select(c => new CategoryLinkViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Children = c.ChildCategories
                    .Where(child => child.IsActive)
                    .OrderBy(child => child.SortOrder)
                    .ThenBy(child => child.Name)
                    .Select(child => new CategoryLinkViewModel
                    {
                        Id = child.Id,
                        Name = child.Name,
                        Slug = child.Slug,
                    })
                    .ToList(),
            })
            .ToListAsync();

        return View(categories);
    }
}
