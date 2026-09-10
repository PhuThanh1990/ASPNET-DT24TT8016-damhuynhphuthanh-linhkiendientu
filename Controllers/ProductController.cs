using System.Text.Json;
using ElectronicStore.Data;
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
    private readonly ApplicationDbContext _context;

    public ProductController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Product list. Search / filter / sort / paging belong to CUS-07: they are applied
    /// to <c>query</c> below, before the projection, so this action keeps one SQL query.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        // TODO (CUS-07): .Where(keyword) / .Where(categoryId, brandId, price range) here.
        // TODO (CUS-07+): ordering option + .Skip()/.Take() for paging here.

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Select(ProductCardViewModel.FromProduct)
            .ToListAsync();

        var model = new ProductListViewModel
        {
            Products = products,
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

        row.Detail.Specifications = ParseSpecifications(row.SpecificationsJson);

        return View(row.Detail);
    }

    /// <summary>
    /// Turns the flat JSON spec object into table rows. Bad or missing JSON is not an
    /// error for the customer: the page simply shows no specification table.
    /// </summary>
    private static List<SpecificationViewModel> ParseSpecifications(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var specifications = new List<SpecificationViewModel>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                // Values should be strings (docs/database-schema.md § 6.1) but a number
                // or a boolean slipped in by hand still renders instead of blowing up.
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                    JsonValueKind.Object or JsonValueKind.Array => string.Empty,
                    _ => property.Value.ToString(),
                };

                if (!string.IsNullOrWhiteSpace(value))
                {
                    specifications.Add(new SpecificationViewModel
                    {
                        Name = property.Name,
                        Value = value,
                    });
                }
            }

            return specifications;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
