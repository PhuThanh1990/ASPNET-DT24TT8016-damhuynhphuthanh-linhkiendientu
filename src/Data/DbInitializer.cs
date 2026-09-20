using ElectronicStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Data;

/// <summary>
/// Brings a fresh database to a usable state: applies pending migrations, then seeds the
/// two Identity roles, the default administrator and the reference catalog.
/// Every step is idempotent, so running it on every startup never duplicates data.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Development-only fallback so a teammate can sign in right after cloning the repo.
    /// Any other environment must supply SeedAdmin:Password through User Secrets or an
    /// environment variable, otherwise no administrator is created at all.
    /// </summary>
    private const string DevelopmentAdminPassword = "Admin@123456";

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));

        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync();

            await SeedRolesAsync(services.GetRequiredService<RoleManager<IdentityRole>>(), logger);
            await SeedAdministratorAsync(
                services.GetRequiredService<UserManager<ApplicationUser>>(),
                app.Configuration,
                app.Environment,
                logger);
            await SeedCatalogAsync(context, logger);
        }
        catch (Exception ex)
        {
            // An unreachable database must not stop the site from starting: the failure is
            // loud in the log and every page that does not touch the database still works.
            logger.LogError(ex, "Database initialization failed. Check the connection string, see README.md.");
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var role in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (result.Succeeded)
            {
                logger.LogInformation("Created role {Role}.", role);
            }
            else
            {
                logger.LogError("Could not create role {Role}: {Errors}", role, Describe(result));
            }
        }
    }

    private static async Task SeedAdministratorAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger logger)
    {
        var email = configuration["SeedAdmin:Email"];
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("SeedAdmin:Email is not configured, skipping the default administrator.");
            return;
        }

        var password = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            if (!environment.IsDevelopment())
            {
                logger.LogWarning(
                    "SeedAdmin:Password is not configured, the default administrator was not created. " +
                    "Set it with User Secrets or an environment variable.");
                return;
            }

            password = DevelopmentAdminPassword;
            logger.LogWarning(
                "Using the built-in development password for {Email}. Set SeedAdmin:Password with " +
                "'dotnet user-secrets set' before using this account anywhere else.", email);
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "Quản trị viên",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                logger.LogError("Could not create the default administrator: {Errors}", Describe(result));
                return;
            }

            logger.LogInformation("Created the default administrator {Email}.", email);
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
        {
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        }
    }

    private static async Task SeedCatalogAsync(ApplicationDbContext context, ILogger logger)
    {
        var now = DateTime.UtcNow;

        var existingCategorySlugs = await context.Categories.Select(c => c.Slug).ToListAsync();
        var newCategories = CatalogSeedData.Categories
            .Where(seed => !existingCategorySlugs.Contains(seed.Slug))
            .Select(seed => new Category
            {
                Name = seed.Name,
                Slug = seed.Slug,
                Description = seed.Description,
                SortOrder = seed.SortOrder,
                IsActive = true,
                CreatedAt = now
            })
            .ToList();

        var existingBrandSlugs = await context.Brands.Select(b => b.Slug).ToListAsync();
        var newBrands = CatalogSeedData.Brands
            .Where(seed => !existingBrandSlugs.Contains(seed.Slug))
            .Select(seed => new Brand
            {
                Name = seed.Name,
                Slug = seed.Slug,
                Description = seed.Description,
                IsActive = true,
                CreatedAt = now
            })
            .ToList();

        if (newCategories.Count > 0 || newBrands.Count > 0)
        {
            context.Categories.AddRange(newCategories);
            context.Brands.AddRange(newBrands);
            await context.SaveChangesAsync();
            logger.LogInformation(
                "Seeded {CategoryCount} categories and {BrandCount} brands.",
                newCategories.Count, newBrands.Count);
        }

        var existingProductSlugs = await context.Products.Select(p => p.Slug).ToListAsync();
        var pendingProducts = CatalogSeedData.Products
            .Where(seed => !existingProductSlugs.Contains(seed.Slug))
            .ToList();

        if (pendingProducts.Count == 0)
        {
            return;
        }

        // Products reference categories and brands by slug, so both lookups happen after
        // the rows above have been saved and have real identity values.
        var categoryIds = await context.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id);
        var brandIds = await context.Brands.ToDictionaryAsync(b => b.Slug, b => b.Id);

        foreach (var seed in pendingProducts)
        {
            if (!categoryIds.TryGetValue(seed.CategorySlug, out var categoryId) ||
                !brandIds.TryGetValue(seed.BrandSlug, out var brandId))
            {
                logger.LogWarning(
                    "Skipped seed product {Slug}: category '{Category}' or brand '{Brand}' is missing.",
                    seed.Slug, seed.CategorySlug, seed.BrandSlug);
                continue;
            }

            context.Products.Add(new Product
            {
                Name = seed.Name,
                Slug = seed.Slug,
                Sku = seed.Sku,
                ShortDescription = seed.ShortDescription,
                Specifications = seed.Specifications,
                Price = seed.Price,
                StockQuantity = seed.StockQuantity,
                CategoryId = categoryId,
                BrandId = brandId,
                IsActive = true,
                CreatedAt = now
            });
        }

        var seeded = await context.SaveChangesAsync();
        logger.LogInformation("Seeded {ProductCount} products ({Rows} rows written).", pendingProducts.Count, seeded);
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));
}
