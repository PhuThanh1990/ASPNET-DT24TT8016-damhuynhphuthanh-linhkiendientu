using ElectronicStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Base class for every controller of the Admin area. It keeps the area name and the role
/// requirement in one place, so an admin screen added later only has to inherit from it and
/// can never be shipped without protection. Anonymous visitors are sent to the login page,
/// signed-in customers get /Account/AccessDenied.
/// </summary>
[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public abstract class AdminControllerBase : Controller
{
}
