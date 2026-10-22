using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.ScheduleChange;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.HallManager)]
public sealed class HallManagerScheduleChangeController : Controller
{
    private readonly IRiversideApiClient _api;
    public HallManagerScheduleChangeController(IRiversideApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index(string? status, string? search, int? id, CancellationToken cancellationToken)
    {
        var token = Request.Cookies["rp_api_token"];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        var result = await _api.GetHallManagerScheduleChangesAsync(token, status, cancellationToken);
        var items = result.Value ?? [];
        if (!string.IsNullOrWhiteSpace(search))
            items = items.Where(x => x.BookingCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        ScheduleChangeViewModel? selected = null;
        if (id.HasValue)
            selected = (await _api.GetHallManagerScheduleChangeAsync(token, id.Value, cancellationToken)).Value;
        ViewBag.Error = result.Error;
        return View(new ScheduleChangeAdminPageViewModel { Items = items, Selected = selected, FilterStatus = status, Search = search });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, string reviewResult, CancellationToken cancellationToken)
    {
        var token = Request.Cookies["rp_api_token"] ?? string.Empty;
        var result = await _api.ReviewScheduleChangeAsync(token, id, reviewResult, cancellationToken);
        return Result(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HallManagerCreateScheduleChangeInput input, CancellationToken cancellationToken)
    {
        var token = Request.Cookies["rp_api_token"] ?? string.Empty;
        var result = await _api.CreateHallManagerScheduleChangeAsync(token, input, cancellationToken);
        return Result(result);
    }

    private JsonResult Result<T>(ApiCallResult<T> result)
    {
        if (result.StatusCode.HasValue) Response.StatusCode = result.StatusCode.Value;
        return Json(new { success = result.Succeeded, data = result.Value, message = result.Error });
    }
}
