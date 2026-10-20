using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Home;
using HeThongDatTiecCuoi_WEB.Models.Coordination;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public sealed class HomeController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;

    public HomeController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(
        bool publicSite = false,
        CancellationToken cancellationToken = default)
    {
        if (!publicSite && User.IsInRole(RoleNames.Admin))
        {
            return RedirectToAction("Dashboard", "AdminAccount");
        }

        if (!publicSite && User.IsInRole(RoleNames.Coordinator))
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var result = await _apiClient.GetFeaturedHallsAsync(cancellationToken);
        return View(new HomeViewModel
        {
            FeaturedHalls = result.Succeeded && result.Value is not null
                ? result.Value
                : []
        });
    }


    [Authorize]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        // Admin truy cập trực tiếp /Home/Dashboard
        // được đưa về trang tổng quan hệ thống.
        if (User.IsInRole(RoleNames.Admin))
        {
            return RedirectToAction("Dashboard", "AdminAccount");
        }

        if (!User.IsInRole(RoleNames.Manager))
        {
            return View(new ManagerDashboardViewModel());
        }

        if (!Request.Cookies.TryGetValue(ApiTokenCookie, out var accessToken) ||
            string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction("Login", "Auth");
        }

        var result = await _apiClient.GetManagerDashboardAsync(accessToken, cancellationToken);
        if (result.Succeeded && result.Value is not null)
        {
            return View(result.Value);
        }

        return View(new ManagerDashboardViewModel
        {
            ErrorMessage = result.Error ?? "Không thể tải dữ liệu tổng quan từ hệ thống."
        });
    }

    [Authorize(Roles = RoleNames.Manager)]
    public IActionResult HallManagerAssignments()
    {
        return RedirectToAction("Index", "ManagerHr");
    }

    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> CoordinationAssignments(string? search, int? hallId, DateTime? eventDate,
        string? coordinationStatus, int page = 1, CancellationToken cancellationToken = default)
    {
        if (!TryGetAccessToken(out var token)) return RedirectToAction("Login", "Auth");
        var result = await _apiClient.GetHallManagerCoordinationAsync(token, search, hallId, eventDate,
            coordinationStatus, page, 10, cancellationToken);
        return View(new HallManagerCoordinationPageViewModel
        {
            Data = result.Value ?? new(), Search = search, HallId = hallId, EventDate = eventDate,
            CoordinationStatus = coordinationStatus,
            ErrorMessage = result.Succeeded ? null : result.Error ?? "Không thể tải danh sách phân công."
        });
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> CoordinationOptions(int bookingId, CancellationToken cancellationToken)
    {
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.GetCoordinatorAvailabilityAsync(token, bookingId, cancellationToken);
        return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> CoordinationUnassignedOptions(CancellationToken cancellationToken)
    {
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.GetHallManagerCoordinationAsync(token, null, null, null,
            "UNASSIGNED", 1, 50, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return StatusCode(result.StatusCode ?? 502, new { message = result.Error });

        var items = result.Value.Items.ToList();
        for (var currentPage = 2; currentPage <= result.Value.TotalPages; currentPage++)
        {
            var nextPage = await _apiClient.GetHallManagerCoordinationAsync(token, null, null, null,
                "UNASSIGNED", currentPage, 50, cancellationToken);
            if (!nextPage.Succeeded || nextPage.Value is null)
                return StatusCode(nextPage.StatusCode ?? 502, new { message = nextPage.Error });
            items.AddRange(nextPage.Value.Items);
        }

        return Json(items.Where(item => item.CanAssign).Select(item => new
        {
            item.BookingId, item.BookingCode, item.CustomerName, item.HallName,
            EventDate = item.EventDate.ToString("dd/MM/yyyy"), item.Shift, item.TableCount
        }));
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> CoordinationHistory(int bookingId, CancellationToken cancellationToken)
    {
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.GetHallManagerCoordinationDetailAsync(token, bookingId, cancellationToken);
        return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> AssignCoordinator(CoordinationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "Tiệc hoặc điều phối viên không hợp lệ." });
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.AssignCoordinatorAsync(token, model.BookingId, model.CoordinatorEmployeeId, cancellationToken);
        return result.Succeeded ? StatusCode(result.StatusCode ?? 200, result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> ChangeCoordinator(CoordinationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Reason))
            return BadRequest(new { message = "Vui lòng chọn điều phối viên và nhập lý do thay đổi." });
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.ChangeCoordinatorAsync(token, model.BookingId, model.CoordinatorEmployeeId,
            model.Reason, cancellationToken);
        return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> CancelCoordinator(CoordinationFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.BookingId < 1 || string.IsNullOrWhiteSpace(model.Reason))
            return BadRequest(new { message = "Vui lòng nhập lý do hủy phân công." });
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.CancelCoordinatorAssignmentAsync(token, model.BookingId, model.Reason, cancellationToken);
        return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [Authorize(Roles = RoleNames.Coordinator)]
    public async Task<IActionResult> AssignedParties(CancellationToken cancellationToken)
    {
        if (!TryGetAccessToken(out var token)) return RedirectToAction("Login", "Auth");
        var result = await _apiClient.GetCoordinatorAssignedPartiesAsync(token, cancellationToken);
        ViewData["ErrorMessage"] = result.Succeeded ? null : result.Error;
        return View(result.Value ?? []);
    }

    [Authorize(Roles = RoleNames.Coordinator)]
    public async Task<IActionResult> AssignedPartyDetail(int bookingId, CancellationToken cancellationToken)
    {
        if (!TryGetAccessToken(out var token)) return RedirectToAction("Login", "Auth");
        var result = await _apiClient.GetCoordinatorAssignedPartiesAsync(token, cancellationToken);
        if (!result.Succeeded) return StatusCode(result.StatusCode ?? 502, result.Error);
        var booking = result.Value?.SingleOrDefault(item => item.BookingId == bookingId);
        return booking is null ? NotFound() : View(booking);
    }


    [HttpGet("khong-co-quyen")]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private bool TryGetAccessToken(out string token)
    {
        token = Request.Cookies[ApiTokenCookie] ?? string.Empty;
        return !string.IsNullOrWhiteSpace(token);
    }
}
