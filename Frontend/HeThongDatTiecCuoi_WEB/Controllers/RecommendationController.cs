using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Models.Recommendation;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public class RecommendationController : Controller
{
    private readonly IRiversideApiClient _apiClient;

    public RecommendationController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // 1. GET: /Recommendation - Tải giao diện khảo sát gói tiệc thông minh
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["PublicActivePage"] = "recommendation";

        var defaultModel = new RecommendationRequestViewModel
        {
            EventDate = DateTime.Today.AddMonths(2),
            Shift = "Ca tối",
            OfficialTableCount = 30,
            SpareTableCount = 2,
            GuestCount = 320,
            ExpectedBudget = 250000000,
            BudgetPriority = "BALANCED",
            DesiredStyle = "Hiện đại / lãng mạn"
        };

        return View(defaultModel);
    }

    // 2. POST: /Recommendation/AnalyzeTop3 - AJAX xử lý thuật toán và trả kết quả
    [HttpPost]
    public async Task<IActionResult> AnalyzeTop3([FromBody] RecommendationRequestViewModel request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Dữ liệu khảo sát chưa hợp lệ, vui lòng kiểm tra lại thông tin." });
        }

        var result = await _apiClient.GetTop3RecommendationsAsync(request, cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            return Json(new { success = false, message = result.Error ?? "Không thể phân tích gói tiệc lúc này." });
        }

        return Json(new { success = true, data = result.Value });
    }
}