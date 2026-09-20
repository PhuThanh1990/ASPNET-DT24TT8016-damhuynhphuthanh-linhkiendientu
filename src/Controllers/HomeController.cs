using System.Diagnostics;
using ElectronicStore.Data;
using ElectronicStore.Models;
using ElectronicStore.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Controllers;

public class HomeController : Controller
{
    /// <summary>How many products each home page row shows.</summary>
    private const int ProductRowSize = 8;

    /// <summary>How many categories the home page grid shows.</summary>
    private const int CategoryGridSize = 8;

    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.ParentCategoryId == null)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Take(CategoryGridSize)
            .Select(c => new CategoryLinkViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ImageUrl = c.ImageUrl,
                // Counted in SQL, so an empty catalog simply reports 0 instead of
                // loading every product just to call Count() in memory.
                ProductCount = c.Products.Count(p => p.IsActive),
            })
            .ToListAsync();

        var featured = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.IsFeatured)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(ProductRowSize)
            .Select(ProductCardViewModel.FromProduct)
            .ToListAsync();

        // Products already shown in the "featured" row are skipped so the two rows do
        // not repeat themselves on a small catalog.
        var featuredIds = featured.Select(p => p.Id).ToList();

        var latest = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && !featuredIds.Contains(p.Id))
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(ProductRowSize)
            .Select(ProductCardViewModel.FromProduct)
            .ToListAsync();

        var model = new HomeViewModel
        {
            Categories = categories,
            FeaturedProducts = featured,
            LatestProducts = latest,
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>
    /// Friendly page for 404 and other error status codes; wired up in Program.cs with
    /// UseStatusCodePagesWithReExecute so the original URL stays in the address bar.
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult HttpError(int statusCode)
    {
        ViewData["StatusCode"] = statusCode;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
