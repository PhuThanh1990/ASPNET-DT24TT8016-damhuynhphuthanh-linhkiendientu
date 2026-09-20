using System.ComponentModel.DataAnnotations;

namespace ElectronicStore.Areas.Admin.Models;

/// <summary>One row of the brand list (ADM-06).</summary>
public class BrandListItemViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Number of products of this brand — a brand in use cannot be deleted.</summary>
    public int ProductCount { get; set; }

    public bool CanDelete => ProductCount == 0;
}

/// <summary>
/// Backing model of the brand Create/Edit form (ADM-07). Audit columns are not part of the
/// form, so they cannot be overposted.
/// </summary>
public class BrandFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "Tên thương hiệu")]
    [Required(ErrorMessage = "Tên thương hiệu là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên thương hiệu tối đa {1} ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Slug")]
    [Required(ErrorMessage = "Slug là bắt buộc.")]
    [StringLength(120, ErrorMessage = "Slug tối đa {1} ký tự.")]
    public string Slug { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    [StringLength(500, ErrorMessage = "Mô tả tối đa {1} ký tự.")]
    public string? Description { get; set; }

    [Display(Name = "Logo (đường dẫn trong wwwroot)")]
    [StringLength(500, ErrorMessage = "Đường dẫn logo tối đa {1} ký tự.")]
    public string? LogoUrl { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;
}

/// <summary>Confirmation screen before deleting a brand (ADM-07).</summary>
public class BrandDeleteViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public int ProductCount { get; set; }

    /// <summary>
    /// Blocked while products still reference the brand: <c>Product.BrandId</c> is
    /// <c>Restrict</c>, products must never be cascaded away with their brand.
    /// </summary>
    public bool CanDelete => ProductCount == 0;
}
