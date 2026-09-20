namespace ElectronicStore.Models;

/// <summary>
/// Product category (CPU, Mainboard, RAM, VGA, SSD, ...).
/// Mapping lives in <c>Data/Configurations/CategoryConfiguration.cs</c>.
/// </summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>URL-friendly unique key, e.g. "cpu-intel".</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Relative path of the category icon/banner under wwwroot.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Null for a root category, otherwise the parent it is nested under.</summary>
    public int? ParentCategoryId { get; set; }

    /// <summary>Menu ordering; lower values are shown first.</summary>
    public int SortOrder { get; set; }

    /// <summary>Soft-delete flag: false hides the category from the shop.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Category? ParentCategory { get; set; }

    public ICollection<Category> ChildCategories { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
