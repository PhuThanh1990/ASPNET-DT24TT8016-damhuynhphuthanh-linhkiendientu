using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElectronicStore.Areas.Admin.Models;

/// <summary>One row of the category list (ADM-04).</summary>
public class CategoryListItemViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? ParentCategoryName { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Number of products pointing at this category — a category in use cannot be deleted.</summary>
    public int ProductCount { get; set; }

    /// <summary>Number of child categories — a parent still holding children cannot be deleted.</summary>
    public int ChildCategoryCount { get; set; }

    public bool CanDelete => ProductCount == 0 && ChildCategoryCount == 0;
}

/// <summary>
/// Backing model of the category Create/Edit form (ADM-05). Only the fields an admin may
/// set are here: <c>CreatedAt</c>, <c>UpdatedAt</c> and the navigation collections stay on
/// the entity, so a crafted POST cannot overwrite them.
/// </summary>
public class CategoryFormViewModel
{
    /// <summary>0 when creating, the existing key when editing.</summary>
    public int Id { get; set; }

    [Display(Name = "Tên danh mục")]
    [Required(ErrorMessage = "Tên danh mục là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên danh mục tối đa {1} ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Slug")]
    [Required(ErrorMessage = "Slug là bắt buộc.")]
    [StringLength(120, ErrorMessage = "Slug tối đa {1} ký tự.")]
    public string Slug { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    [StringLength(500, ErrorMessage = "Mô tả tối đa {1} ký tự.")]
    public string? Description { get; set; }

    [Display(Name = "Ảnh (đường dẫn trong wwwroot)")]
    [StringLength(500, ErrorMessage = "Đường dẫn ảnh tối đa {1} ký tự.")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Danh mục cha")]
    public int? ParentCategoryId { get; set; }

    [Display(Name = "Thứ tự hiển thị")]
    [Range(0, int.MaxValue, ErrorMessage = "Thứ tự hiển thị không được âm.")]
    public int SortOrder { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Dropdown data, refilled by the controller on every render. Never model-bound, so a
    /// POST cannot inject options and it is not validated.
    /// </summary>
    [BindNever]
    [ValidateNever]
    public IEnumerable<SelectListItem> ParentCategoryOptions { get; set; } = new List<SelectListItem>();
}

/// <summary>Confirmation screen before deleting a category (ADM-05).</summary>
public class CategoryDeleteViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public int ProductCount { get; set; }

    public int ChildCategoryCount { get; set; }

    /// <summary>
    /// Deleting is blocked while anything still references the row: the foreign keys are
    /// <c>Restrict</c> on purpose, products must never be cascaded away with their category.
    /// </summary>
    public bool CanDelete => ProductCount == 0 && ChildCategoryCount == 0;
}
