using ElectronicStore.Areas.Admin.Models;
using ElectronicStore.Areas.Admin.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Landing page of the Admin area (<c>/Admin</c>): the KPI dashboard, the revenue chart,
/// best sellers and the newest orders (ADM-17 → ADM-19).
/// </summary>
/// <remarks>
/// Read-only by design — the dashboard reports on data the Core order service produced and
/// never changes an order, a status or stock.
/// </remarks>
public class HomeController : AdminControllerBase
{
    private readonly DashboardService _dashboard;

    public HomeController(DashboardService dashboard) => _dashboard = dashboard;

    // GET /Admin?days=30
    public async Task<IActionResult> Index(int days, CancellationToken cancellationToken)
    {
        // days = 0 when the query string is absent; the service falls back to its default and
        // rejects anything outside the offered ranges.
        var model = await _dashboard.BuildAsync(days, cancellationToken);

        return View(model);
    }
}
