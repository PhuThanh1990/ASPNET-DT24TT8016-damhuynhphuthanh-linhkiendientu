using ElectronicStore.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Data;

/// <summary>
/// EF Core database context for the ElectronicStore application.
/// Derives from <see cref="IdentityDbContext{TUser}"/> so ASP.NET Core Identity owns the
/// AspNet* tables, while the shop's own tables are mapped from Data/Configurations.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<Address> Addresses => Set<Address>();

    public DbSet<ShippingAddress> ShippingAddresses => Set<ShippingAddress>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Maps the Identity tables first, then layers this project's configuration on top.
        base.OnModelCreating(modelBuilder);

        // Picks up every IEntityTypeConfiguration<T> in this assembly, so per-entity
        // mapping lives in Data/Configurations instead of piling up in this method.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
