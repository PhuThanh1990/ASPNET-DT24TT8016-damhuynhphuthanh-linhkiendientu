using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElectronicStore.Areas.Admin.Models;

/// <summary>One row of the admin product list (ADM-08).</summary>
public class ProductListItemViewModel
{
    public int Id { get; set; }

    /// <summary>Primary image if the product has one, otherwise the first gallery image.</summary>
    public string? PrimaryImageUrl { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string BrandName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Backing model of the product Create form (ADM-09). It carries only the columns an admin
/// fills in — <c>ViewCount</c>, <c>CreatedAt</c> and <c>UpdatedAt</c> are set by the server,
/// so they cannot be overposted.
/// </summary>
public class ProductCreateViewModel
{
    [Display(Name = "Tên sản phẩm")]
    [Required(ErrorMessage = "Tên sản phẩm là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tên sản phẩm tối đa {1} ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Slug")]
    [Required(ErrorMessage = "Slug là bắt buộc.")]
    [StringLength(220, ErrorMessage = "Slug tối đa {1} ký tự.")]
    public string Slug { get; set; } = string.Empty;

    // Sku is NOT NULL and uniquely indexed in the database, so the create form has to ask
    // for it even though the task description lists it as optional.
    [Display(Name = "SKU")]
    [Required(ErrorMessage = "SKU là bắt buộc.")]
    [StringLength(50, ErrorMessage = "SKU tối đa {1} ký tự.")]
    public string Sku { get; set; } = string.Empty;

    [Display(Name = "Mô tả ngắn")]
    [StringLength(500, ErrorMessage = "Mô tả ngắn tối đa {1} ký tự.")]
    public string? ShortDescription { get; set; }

    [Display(Name = "Mô tả chi tiết")]
    public string? Description { get; set; }

    [Display(Name = "Thông số kỹ thuật (JSON)")]
    public string? Specifications { get; set; }

    // Matches decimal(18,2) and the CK_Products_Price check constraint.
    [Display(Name = "Giá bán")]
    [Required(ErrorMessage = "Giá bán là bắt buộc.")]
    [Range(typeof(decimal), "0", "9999999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Giá bán phải là số không âm.")]
    public decimal Price { get; set; }

    [Display(Name = "Giá gốc (nếu đang giảm giá)")]
    [Range(typeof(decimal), "0", "9999999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Giá gốc phải là số không âm.")]
    public decimal? OldPrice { get; set; }

    [Display(Name = "Số lượng tồn kho")]
    [Required(ErrorMessage = "Số lượng tồn kho là bắt buộc.")]
    [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho không được âm.")]
    public int StockQuantity { get; set; }

    [Display(Name = "Danh mục")]
    [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục.")]
    public int CategoryId { get; set; }

    [Display(Name = "Thương hiệu")]
    [Required(ErrorMessage = "Vui lòng chọn thương hiệu.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn thương hiệu.")]
    public int BrandId { get; set; }

    [Display(Name = "Đang bán")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Sản phẩm nổi bật")]
    public bool IsFeatured { get; set; }

    /// <summary>Dropdown data, refilled by the controller on every render; never model-bound.</summary>
    [BindNever]
    [ValidateNever]
    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = new List<SelectListItem>();

    /// <summary>Dropdown data, refilled by the controller on every render; never model-bound.</summary>
    [BindNever]
    [ValidateNever]
    public IEnumerable<SelectListItem> BrandOptions { get; set; } = new List<SelectListItem>();
}
