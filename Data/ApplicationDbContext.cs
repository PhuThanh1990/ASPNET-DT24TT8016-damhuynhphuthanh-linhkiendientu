using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Data;

/// <summary>
/// EF Core database context for the ElectronicStore application.
/// Catalog entities are mapped here; cart/order and identity entities are added
/// as the corresponding tasks are implemented.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Picks up every IEntityTypeConfiguration<T> in this assembly, so per-entity
        // mapping lives in Data/Configurations instead of piling up in this method.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
