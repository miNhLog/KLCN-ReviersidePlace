using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.ScheduleChange;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerScheduleChangeController : Controller
{
    private readonly IRiversideApiClient _api;
    public ManagerScheduleChangeController(IRiversideApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index(string? status, string? search, int? id, CancellationToken cancellationToken)
    {
        var token = Request.Cookies["rp_api_token"];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        var result = await _api.GetManagerScheduleChangesAsync(token, status, cancellationToken);
        var items = result.Value ?? [];
        if (!string.IsNullOrWhiteSpace(search))
            items = items.Where(x => x.BookingCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        ScheduleChangeViewModel? selected = null;
        ScheduleChangeFinancialViewModel? financials = null;
        if (id.HasValue)
        {
            selected = (await _api.GetManagerScheduleChangeAsync(token, id.Value, cancellationToken)).Value;
            financials = (await _api.GetScheduleChangeFinancialsAsync(token, id.Value, cancellationToken)).Value;
        }
        ViewBag.Error = result.Error;
        return View(new ScheduleChangeAdminPageViewModel
        { Items = items, Selected = selected, Financials = financials, FilterStatus = status, Search = search });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Quote(int id, string? note, CancellationToken cancellationToken) =>
        Result(await _api.QuoteScheduleChangeAsync(Token(), id, note, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason, CancellationToken cancellationToken) =>
        Result(await _api.RejectScheduleChangeAsync(Token(), id, reason, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reconcile(int id, string action, string evidence, CancellationToken cancellationToken) =>
        Result(await _api.ReconcileScheduleChangeAsync(Token(), id, action, evidence, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmRefund(int id, int refundId, string evidence, CancellationToken cancellationToken) =>
        Result(await _api.ConfirmScheduleChangeRefundAsync(Token(), id, refundId, evidence, cancellationToken));

    private string Token() => Request.Cookies["rp_api_token"] ?? string.Empty;
    private JsonResult Result<T>(ApiCallResult<T> result)
    {
        if (result.StatusCode.HasValue) Response.StatusCode = result.StatusCode.Value;
        return Json(new { success = result.Succeeded, data = result.Value, message = result.Error });
    }
}
