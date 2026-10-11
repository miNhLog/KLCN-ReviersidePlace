using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Models.Recommendation;
using HeThongDatTiecCuoi_WEB.Models.AdminBooking;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public class RecommendationController : Controller
{
    private readonly IRiversideApiClient _apiClient;
    private readonly ILogger<RecommendationController> _logger;

    public RecommendationController(IRiversideApiClient apiClient, ILogger<RecommendationController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["PublicActivePage"] = "recommendation";

        var defaultModel = new RecommendationRequestViewModel
        {
            EventDate = DateTime.Today.AddMonths(2),
            Shift = "Ca tối",
            OfficialTableCount = 25,
            SpareTableCount = 2,
            GuestCount = 270,
            ExpectedBudget = 250000000,
            BudgetPriority = "BALANCED",
            DesiredStyle = "Hiện đại / lãng mạn"
        };

        // Nếu người dùng đã đăng nhập, tự động trích xuất thông tin
        if (User.Identity?.IsAuthenticated == true)
        {
            defaultModel.Email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name;
            defaultModel.CoupleName = User.FindFirstValue(ClaimTypes.Name) ?? "";
            defaultModel.PhoneNumber = User.FindFirstValue(ClaimTypes.MobilePhone) ?? User.FindFirstValue("phone") ?? "";

            // Lấy token từ Cookie an toàn (không gọi HttpContext.Session)
            var token = Request.Cookies["accessToken"];
            if (!string.IsNullOrEmpty(token) && string.IsNullOrEmpty(defaultModel.PhoneNumber))
            {
                var userRes = await _apiClient.GetCurrentUserAsync(token, cancellationToken);
                if (userRes.Succeeded && userRes.Value != null)
                {
                    defaultModel.CoupleName = string.IsNullOrEmpty(defaultModel.CoupleName) ? userRes.Value.FullName : defaultModel.CoupleName;
                    defaultModel.PhoneNumber = userRes.Value.PhoneNumber ?? defaultModel.PhoneNumber;
                    defaultModel.Email = userRes.Value.Email ?? defaultModel.Email;
                }
            }
        }

        return View(defaultModel);
    }

    [HttpPost]
    public async Task<IActionResult> AnalyzeTop3([FromBody] RecommendationRequestViewModel request, CancellationToken cancellationToken = default)
    {
        // Try-catch bao bọc TOÀN BỘ action Frontend: log full ex.ToString(),
        // không để exception văng 500; trả JSON thân thiện cho JS alert.
        try
        {
        if (request is null)
        {
            ViewBag.RecommendationError = "Thiếu dữ liệu khảo sát.";
            return Json(new { success = false, message = "Thiếu dữ liệu khảo sát, vui lòng nhập lại." });
        }
        if (!ModelState.IsValid)
        {
            ViewBag.RecommendationError = "Dữ liệu khảo sát chưa hợp lệ.";
            return Json(new { success = false, message = "Dữ liệu khảo sát chưa hợp lệ, vui lòng kiểm tra lại." });
        }

        var result = await _apiClient.GetTop3RecommendationsAsync(request, cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            _logger.LogWarning("AnalyzeTop3: Backend trả lỗi: {Error}", result.Error);
            ViewBag.RecommendationError = result.Error;
            return Json(new { success = false, message = result.Error ?? "Không thể phân tích gói tiệc lúc này, vui lòng thử lại sau." });
        }

        return Json(new { success = true, data = result.Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnalyzeTop3 thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            ViewBag.RecommendationError = "Hệ thống gợi ý đang bận, vui lòng thử lại sau.";
            return Json(new { success = false, message = "Hệ thống gợi ý đang bận, vui lòng thử lại sau.", traceId = HttpContext.TraceIdentifier });
        }
    }

    // Lấy ma trận lịch sảnh trống
    [HttpGet]
    public async Task<IActionResult> GetHallAvailability(string? date, CancellationToken cancellationToken)
    {
        try
        {
            var queryDate = string.IsNullOrWhiteSpace(date) ? DateTime.Today.AddMonths(2).ToString("yyyy-MM-dd") : date;
            var result = await _apiClient.GetHallAvailabilityRealtimeAsync(queryDate, cancellationToken);

            if (result.Succeeded && result.Value != null)
            {
                result.Value.QueryDate ??= queryDate;
                result.Value.Halls ??= new List<HeThongDatTiecCuoi_WEB.Models.Recommendation.HallSlotItemViewModel>();
                return Json(new { success = true, data = result.Value });
            }

            return Json(new { success = false, message = result.Error ?? "Không thể tải dữ liệu lịch sảnh từ cơ sở dữ liệu." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetHallAvailability thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            // Null-safe: trả ViewModel rỗng thay vì throw để modal không vỡ với lỗi convert Path $.
            return Json(new
            {
                success = true,
                data = new HallAvailabilityResponseViewModel
                {
                    QueryDate = date ?? string.Empty,
                    Halls = new List<HallSlotItemViewModel>()
                }
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RegisterBooking([FromBody] PublicBookingRequestViewModel request, CancellationToken cancellationToken)
    {
        // 1. Tự động lấy UserId của tài khoản đang đăng nhập
        if (User.Identity?.IsAuthenticated == true)
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idClaim, out var uid))
            {
                request.UserId = uid;
            }
        }

        var result = await _apiClient.RegisterPublicBookingAsync(request, cancellationToken);

        if (result.Succeeded)
        {
            // 2. Tự động lưu SĐT vào Cookie để khi chuyển sang trang "Tiệc của tôi" sẽ tự nhận diện
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                Response.Cookies.Append("customer_phone", request.PhoneNumber.Trim(), new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(30),
                    IsEssential = true
                });
            }

            return Json(new { success = true, message = "Đăng ký giữ chỗ thành công!", bookingCode = result.Value });
        }

        return Json(new { success = false, message = result.Error ?? "Không thể lưu đơn đăng ký tiệc." });
    }
}