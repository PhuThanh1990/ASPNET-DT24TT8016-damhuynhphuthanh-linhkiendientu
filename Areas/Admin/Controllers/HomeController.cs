using Microsoft.AspNetCore.Mvc;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Landing page of the Admin area (<c>/Admin</c>). Deliberately a placeholder:
/// the KPI dashboard is a later task, this only gives the area a valid default route.
/// </summary>
public class HomeController : AdminControllerBase
{
    public IActionResult Index() => View();
}
