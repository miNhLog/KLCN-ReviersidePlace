using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Recommendation;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Controllers;

[Route("api/recommendations")]
[ApiController]
public sealed class RecommendationController : ControllerBase
{
    private const double BudgetWeight = 35d / 85d;
    private const double SpaceWeight = 25d / 85d;
    private const double StyleWeight = 25d / 85d;

    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiServiceOptions _aiOptions;
    private readonly ILogger<RecommendationController> _logger;

    // Python FastAPI trả snake_case (top_3, hall_id, total_estimated_cost...).
    // C# dùng PascalCase -> bắt buộc case-insensitive + JsonPropertyName đầy đủ,
    // nếu không deserialize ra null và dễ gây 500 ở tầng trên.
    private static readonly JsonSerializerOptions AiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public RecommendationController(
        ApplicationDbContext context,
        IStatusService statusService,
        IHttpClientFactory httpClientFactory,
        IOptions<AiServiceOptions> aiOptions,
        ILogger<RecommendationController> logger)
    {
        _context = context;
        _statusService = statusService;
        _httpClientFactory = httpClientFactory;
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gọi AI Microservice (FastAPI WSM, port 8000) để xếp hạng Top-3 gói tiệc.
    /// Truy vấn LichSanh (HallSchedules) lấy SanhTiecID đã đặt truyền occupied_hall_ids.
    /// Fallback heuristic nội bộ nếu AI timeout (&gt;5s) hoặc lỗi.
    /// </summary>
    [HttpPost("suggest-top3-ai")]
    public async Task<IActionResult> SuggestTop3ViaAi([FromBody] RecommendationRequestDto? request)
    {
        // Try-catch bao bọc TOÀN BỘ action: bất kỳ Exception nào cũng log full
        // ex.ToString() kèm traceId rồi trả fallback 200 hoặc 500 có traceId,
        // không để UseExceptionHandler trả 500 ProblemDetails rỗng.
        try
        {
        if (request is null)
            return BadRequest(new { message = "Thiếu dữ liệu khảo sát trong body." });

        // Null-safe: DTO mặc định string.Empty nhưng JSON có thể gửi null tường minh.
        var desiredShift = (request.DesiredShift ?? string.Empty).Trim();
        var desiredStyle = (request.DesiredStyle ?? string.Empty).Trim();
        if (desiredShift != HallShiftNames.Lunch && desiredShift != HallShiftNames.Dinner)
            return BadRequest(new { message = "Ca tổ chức chỉ có thể là Ca trưa hoặc Ca tối." });
        if (string.IsNullOrWhiteSpace(desiredStyle))
            return BadRequest(new { message = "Phong cách mong muốn không được để trống." });
        if (request.GuestCount <= 0)
            return BadRequest(new { message = "Số lượng khách phải lớn hơn 0." });

        // Parse ngày an toàn, chuẩn hóa về 00:00. So sánh theo khoảng [targetDate, nextDay)
        // để tránh lệch giờ SQL datetime và lỗi DateOnly trên Windows.
        DateTime targetDate;
        try
        {
            targetDate = request.DesiredDate.Date;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DesiredDate không hợp lệ: {Raw}", request.DesiredDate);
            return BadRequest(new { message = "Ngày tổ chức không hợp lệ." });
        }
        var nextDay = targetDate.AddDays(1);

        byte existingDataStatusId;
        List<int> occupiedHallIds = new();
        try
        {
            existingDataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);

            // LichSanh đã đặt (BOOKED/LOCKED) trong ngày + ca -> occupied_hall_ids cho AI.
            // Null-safe: Status/Hall navigation non-null trong model (null!) nhưng vẫn
            // bọc try-catch để SqlException/NullReference không văng 500 ra UseExceptionHandler.
            occupiedHallIds = await _context.HallSchedules.AsNoTracking()
                .Where(schedule =>
                    schedule.Date >= targetDate && schedule.Date < nextDay &&
                    schedule.Shift == desiredShift &&
                    schedule.DataStatusId == existingDataStatusId &&
                    schedule.Status != null &&
                    schedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                    (schedule.Status.StatusCode == HallScheduleStatusCodes.Booked ||
                     schedule.Status.StatusCode == HallScheduleStatusCodes.Locked))
                .Select(schedule => schedule.HallId)
                .Distinct()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            // Lỗi DB trước khi gọi AI: log chi tiết + fallback nội bộ để vẫn trả 200.
            _logger.LogError(ex, "SuggestTop3ViaAi: lỗi truy vấn occupied_hall_ids (date={Date}, shift={Shift}, traceId={TraceId}). Chi tiết: {Detail}",
                targetDate.ToString("yyyy-MM-dd"), desiredShift, HttpContext.TraceIdentifier, ex.ToString());
            try
            {
                var safeStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
                var tableCountSafe = (int)Math.Ceiling((request.GuestCount <= 0 ? 1 : request.GuestCount) / 10m);
                var fallbackEarly = await BuildFallbackTop3Async(request, desiredShift, desiredStyle,
                    targetDate, nextDay, safeStatusId, tableCountSafe);
                return Ok(new
                {
                    success = true,
                    source = "fallback-internal",
                    aiBaseUrl = _aiOptions.BaseUrl,
                    aiError = "DB query failed before AI call: " + ex.Message,
                    traceId = HttpContext.TraceIdentifier,
                    occupiedHallIds,
                    data = fallbackEarly
                });
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "SuggestTop3ViaAi: fallback sớm cũng thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                    HttpContext.TraceIdentifier, inner.ToString());
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Không thể tạo gợi ý lúc này, vui lòng thử lại.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
        }

        var tableCount = (int)Math.Ceiling(request.GuestCount / 10m);
        var aiShift = desiredShift == HallShiftNames.Lunch ? "Ca trua" : "Ca toi";
        var priorityServices = (request.ServiceNeeds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var aiPayload = new
        {
            guest_count = request.GuestCount,
            table_count = tableCount,
            expected_budget = request.ExpectedBudget,
            event_date = targetDate.ToString("yyyy-MM-dd"),
            shift = aiShift,
            desired_style = desiredStyle,
            priority_services = priorityServices,
            occupied_hall_ids = occupiedHallIds
        };

        try
        {
            var client = _httpClientFactory.CreateClient("AiService");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_aiOptions.TimeoutSeconds));
            using var response = await client.PostAsJsonAsync("api/v1/ai/wsm/rank", aiPayload, cts.Token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cts.Token);
            // Validate snake_case bằng DTO có [JsonPropertyName] + case-insensitive.
            // Nếu Python đổi contract mà parse ra null -> rơi xuống fallback thay vì trả null cho client.
            AiRankResponse? typed = null;
            try
            {
                typed = JsonSerializer.Deserialize<AiRankResponse>(json, AiJsonOptions);
            }
            catch (JsonException jex)
            {
                _logger.LogWarning(jex, "SuggestTop3ViaAi: AI trả JSON không đúng contract snake_case. traceId={TraceId}. Body: {Body}",
                    HttpContext.TraceIdentifier, json.Length > 2000 ? json[..2000] : json);
            }
            if (typed is null || typed.Top3 is null)
            {
                _logger.LogWarning("SuggestTop3ViaAi: AI deserialize ra null (snake_case mismatch?). traceId={TraceId}. Body: {Body}",
                    HttpContext.TraceIdentifier, json.Length > 2000 ? json[..2000] : json);
            }
            using var doc = JsonDocument.Parse(json);
            return Ok(new
            {
                success = true,
                source = "ai-service",
                aiBaseUrl = _aiOptions.BaseUrl,
                occupiedHallIds,
                data = JsonSerializer.Deserialize<object>(json, AiJsonOptions)
            });
        }
        catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException || ex is JsonException)
        {
            _logger.LogWarning(ex, "SuggestTop3ViaAi: AI service lỗi ({BaseUrl}), dùng fallback nội bộ. traceId={TraceId}. Chi tiết: {Detail}",
                _aiOptions.BaseUrl, HttpContext.TraceIdentifier, ex.ToString());
            // Fallback heuristic nội bộ khi AI timeout (>5s) hoặc lỗi.
            try
            {
                var fallback = await BuildFallbackTop3Async(request, desiredShift, desiredStyle,
                    targetDate, nextDay, existingDataStatusId, tableCount);
                return Ok(new
                {
                    success = true,
                    source = "fallback-internal",
                    aiBaseUrl = _aiOptions.BaseUrl,
                    aiError = ex.Message,
                    traceId = HttpContext.TraceIdentifier,
                    occupiedHallIds,
                    data = fallback
                });
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "SuggestTop3ViaAi: fallback nội bộ thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                    HttpContext.TraceIdentifier, fallbackEx.ToString());
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Không thể tạo gợi ý lúc này, vui lòng thử lại.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
        }
        } // end outer try
        catch (Exception ex)
        {
            // Lưới an toàn cuối: mọi lỗi ngoài dự kiến (payload, HttpClient config, ...)
            // đều log full ex.ToString() + traceId và trả 500 có traceId, không để 500 rỗng.
            _logger.LogError(ex, "SuggestTop3ViaAi: lỗi ngoài dự kiến. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            try
            {
                if (request is not null)
                {
                    var desiredShiftFb = (request.DesiredShift ?? string.Empty).Trim();
                    var desiredStyleFb = (request.DesiredStyle ?? string.Empty).Trim();
                    DateTime targetDateFb;
                    try { targetDateFb = request.DesiredDate.Date; }
                    catch { targetDateFb = DateTime.Today; }
                    var nextDayFb = targetDateFb.AddDays(1);
                    var statusIdFb = await GetDataStatusIdAsync(DataStatusCodes.Existing);
                    var tableCountFb = (int)Math.Ceiling((request.GuestCount <= 0 ? 1 : request.GuestCount) / 10m);
                    var fallback = await BuildFallbackTop3Async(request, desiredShiftFb, desiredStyleFb,
                        targetDateFb, nextDayFb, statusIdFb, tableCountFb);
                    return Ok(new
                    {
                        success = true,
                        source = "fallback-internal",
                        aiBaseUrl = _aiOptions.BaseUrl,
                        aiError = "Unexpected: " + ex.Message,
                        traceId = HttpContext.TraceIdentifier,
                        data = fallback
                    });
                }
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "SuggestTop3ViaAi: fallback cuối cũng thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                    HttpContext.TraceIdentifier, fallbackEx.ToString());
            }
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể tạo gợi ý lúc này, vui lòng thử lại.",
                detail = ex.Message,
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    private async Task<object> BuildFallbackTop3Async(
        RecommendationRequestDto request, string desiredShift, string desiredStyle,
        DateTime targetDate, DateTime nextDay, byte existingDataStatusId, int tableCount)
    {
        var style = (desiredStyle ?? request.DesiredStyle ?? string.Empty).Trim();
        var candidateSchedules = await _context.HallSchedules
            .AsNoTracking()
            .Where(schedule =>
                schedule.Date >= targetDate && schedule.Date < nextDay &&
                schedule.Shift == desiredShift &&
                schedule.DataStatusId == existingDataStatusId &&
                schedule.Status != null &&
                schedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                schedule.Status.StatusCode == HallScheduleStatusCodes.Available &&
                schedule.Hall != null &&
                schedule.Hall.DataStatusId == existingDataStatusId &&
                schedule.Hall.Status != null &&
                schedule.Hall.Status.StatusGroup == StatusGroups.Hall &&
                schedule.Hall.Status.StatusCode == HallStatusCodes.Active &&
                request.GuestCount <= schedule.Hall.MaximumCapacity &&
                (!schedule.Hall.MinimumCapacity.HasValue || request.GuestCount >= schedule.Hall.MinimumCapacity.Value))
            .Select(schedule => new { schedule.HallScheduleId, Hall = schedule.Hall })
            .ToListAsync();

        var activeMenus = await _context.Menus.AsNoTracking()
            .Where(menu =>
                menu.MenuType == MenuTypes.Standard &&
                menu.DataStatusId == existingDataStatusId &&
                menu.Status != null &&
                menu.Status.StatusGroup == StatusGroups.Menu &&
                menu.Status.StatusCode == MenuStatusCodes.Active)
            .ToListAsync();

        var activeDecors = await _context.DecorPackages.AsNoTracking()
            .Where(decor =>
                decor.DataStatusId == existingDataStatusId &&
                decor.Status != null &&
                decor.Status.StatusGroup == StatusGroups.DecorationPackage &&
                decor.Status.StatusCode == BusinessStatusCodes.Active)
            .ToListAsync();

        var scored = new List<ComboSuggestionDto>();
        foreach (var schedule in candidateSchedules)
        foreach (var menu in activeMenus)
        foreach (var decor in activeDecors)
        {
            var hall = schedule.Hall;
            var menuTotal = menu.PricePerTable * tableCount;
            var totalCost = hall.RentalPrice + menuTotal + decor.Gia;
            var budgetDifference = request.ExpectedBudget - totalCost;
            var budgetScore = CalculateBudgetScore(totalCost, request.ExpectedBudget);
            var spaceScore = CalculateSpaceScore(request.GuestCount, hall.MaximumCapacity);
            var styleScore = CalculateStyleScore(style, decor.PhongCach);
            var overallScore = Math.Round(
                budgetScore * BudgetWeight + spaceScore * SpaceWeight + styleScore * StyleWeight, 1);
            scored.Add(new ComboSuggestionDto
            {
                OverallMatchScore = overallScore,
                BudgetMatchScore = Math.Round(budgetScore, 1),
                SpaceFitScore = Math.Round(spaceScore, 1),
                StyleThemeScore = Math.Round(styleScore, 1),
                HallScheduleId = schedule.HallScheduleId,
                HallId = hall.HallId,
                HallName = hall.HallName,
                HallCode = hall.HallCode,
                HallCapacity = hall.MaximumCapacity,
                HallRentalPrice = hall.RentalPrice,
                TotalEstimatedCost = totalCost,
                BudgetDifference = budgetDifference,
                MenuId = menu.MenuId,
                MenuName = menu.MenuName,
                MenuPricePerTable = menu.PricePerTable,
                EstimatedTableCount = tableCount,
                MenuTotalPrice = menuTotal,
                DecorationPackageId = decor.GoiTrangTriID,
                DecorationPackageName = decor.TenGoi,
                DecorationStyle = decor.PhongCach,
                DecorationPrice = decor.Gia,
                BudgetAnalysisText = budgetDifference >= 0 ? "Trong ngân sách" : "Vượt ngân sách",
                RecommendationReasons = new List<string>
                {
                    $"Sảnh {hall.HallName} còn trống và phù hợp {request.GuestCount} khách (fallback nội bộ)."
                }
            });
        }

        var top = scored.OrderByDescending(combo => combo.OverallMatchScore).Take(3).ToList();
        for (var index = 0; index < top.Count; index++)
            top[index].Rank = index + 1;
        return new
        {
            totalCandidatesEvaluated = scored.Count,
            top3 = top,
            algorithmNotice = $"Fallback heuristic nội bộ: đã đánh giá {scored.Count} phương án (AI timeout >{_aiOptions.TimeoutSeconds}s hoặc lỗi)."
        };
    }

    [HttpPost("suggest-top3")]
    public async Task<IActionResult> SuggestTop3Combos([FromBody] RecommendationRequestDto? request)
    {
        if (request is null)
            return BadRequest(new { message = "Thiếu dữ liệu khảo sát trong body." });

        // Null-safe: JSON có thể gửi null tường minh dù DTO có default string.Empty.
        var desiredShift = (request.DesiredShift ?? string.Empty).Trim();
        var desiredStyle = (request.DesiredStyle ?? string.Empty).Trim();
        if (desiredShift != HallShiftNames.Lunch && desiredShift != HallShiftNames.Dinner)
            return BadRequest(new { message = "Ca tổ chức chỉ có thể là Ca trưa hoặc Ca tối." });
        if (string.IsNullOrWhiteSpace(desiredStyle))
            return BadRequest(new { message = "Phong cách mong muốn không được để trống." });
        if (request.GuestCount <= 0)
            return BadRequest(new { message = "Số lượng khách phải lớn hơn 0." });

        DateTime targetDate;
        try
        {
            targetDate = request.DesiredDate.Date;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DesiredDate không hợp lệ.");
            return BadRequest(new { message = "Ngày tổ chức không hợp lệ." });
        }
        var nextDay = targetDate.AddDays(1);

        try
        {
        var existingDataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var customerId = await ResolveAuthenticatedCustomerIdAsync(existingDataStatusId);
        var requestEntity = new RecommendationRequestEntity
        {
            CustomerId = customerId,
            ExpectedBudget = request.ExpectedBudget,
            GuestCount = request.GuestCount,
            DesiredDate = targetDate,
            DesiredShift = desiredShift,
            DesiredStyle = desiredStyle,
            ServiceNeeds = NormalizeOptional(request.ServiceNeeds),
            CreatedAt = DateTime.Now
        };
        _context.RecommendationRequests.Add(requestEntity);
        await _context.SaveChangesAsync();

        var candidateSchedules = await _context.HallSchedules
            .AsNoTracking()
            .Where(schedule =>
                schedule.Date >= targetDate && schedule.Date < nextDay &&
                schedule.Shift == desiredShift &&
                schedule.DataStatusId == existingDataStatusId &&
                schedule.Status != null &&
                schedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                schedule.Status.StatusCode == HallScheduleStatusCodes.Available &&
                schedule.Hall != null &&
                schedule.Hall.DataStatusId == existingDataStatusId &&
                schedule.Hall.Status != null &&
                schedule.Hall.Status.StatusGroup == StatusGroups.Hall &&
                schedule.Hall.Status.StatusCode == HallStatusCodes.Active &&
                request.GuestCount <= schedule.Hall.MaximumCapacity &&
                (!schedule.Hall.MinimumCapacity.HasValue || request.GuestCount >= schedule.Hall.MinimumCapacity.Value))
            .Select(schedule => new
            {
                schedule.HallScheduleId,
                Hall = schedule.Hall
            })
            .ToListAsync();

        if (candidateSchedules.Count == 0)
        {
            return Ok(new RecommendationResponseDto
            {
                RecommendationRequestId = requestEntity.RecommendationRequestId,
                TotalCandidatesEvaluated = 0,
                AlgorithmNotice = "Không có lịch sảnh AVAILABLE phù hợp ngày, ca và số lượng khách đã chọn.",
                AnalyzedAt = DateTime.Now
            });
        }

        var activeMenus = await _context.Menus.AsNoTracking()
            .Where(menu =>
                menu.MenuType == MenuTypes.Standard &&
                menu.DataStatusId == existingDataStatusId &&
                menu.Status != null &&
                menu.Status.StatusGroup == StatusGroups.Menu &&
                menu.Status.StatusCode == MenuStatusCodes.Active)
            .ToListAsync();

        var activeDecors = await _context.DecorPackages.AsNoTracking()
            .Where(decor =>
                decor.DataStatusId == existingDataStatusId &&
                decor.Status != null &&
                decor.Status.StatusGroup == StatusGroups.DecorationPackage &&
                decor.Status.StatusCode == BusinessStatusCodes.Active)
            .Select(decor => new
            {
                Decor = decor,
                ImageUrl = decor.Images
                    .Where(image => image.IsPrimary && image.DataStatusId == existingDataStatusId)
                    .Select(image => image.ImagePath)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var estimatedTableCount = (int)Math.Ceiling(request.GuestCount / 10m);
        var scoredCombos = new List<ComboSuggestionDto>();

        foreach (var schedule in candidateSchedules)
        foreach (var menu in activeMenus)
        foreach (var decorCandidate in activeDecors)
        {
            var hall = schedule.Hall;
            var decor = decorCandidate.Decor;
            var menuTotal = menu.PricePerTable * estimatedTableCount;
            var totalCost = hall.RentalPrice + menuTotal + decor.Gia;
            var budgetDifference = request.ExpectedBudget - totalCost;
            var budgetScore = CalculateBudgetScore(totalCost, request.ExpectedBudget);
            var spaceScore = CalculateSpaceScore(request.GuestCount, hall.MaximumCapacity);
            var styleScore = CalculateStyleScore(desiredStyle, decor.PhongCach);
            var overallScore = Math.Round(
                budgetScore * BudgetWeight + spaceScore * SpaceWeight + styleScore * StyleWeight,
                1);

            var reasons = new List<string>
            {
                $"Sảnh {hall.HallName} còn trống và phù hợp {request.GuestCount} khách.",
                budgetDifference >= 0
                    ? $"Phương án thấp hơn ngân sách dự kiến {budgetDifference:N0} VNĐ."
                    : $"Phương án vượt ngân sách dự kiến {Math.Abs(budgetDifference):N0} VNĐ."
            };
            if (styleScore > 70)
                reasons.Add($"Phong cách {decor.PhongCach} phù hợp với mong muốn {desiredStyle}.");

            scoredCombos.Add(new ComboSuggestionDto
            {
                OverallMatchScore = overallScore,
                BudgetMatchScore = Math.Round(budgetScore, 1),
                SpaceFitScore = Math.Round(spaceScore, 1),
                StyleThemeScore = Math.Round(styleScore, 1),
                HallScheduleId = schedule.HallScheduleId,
                HallId = hall.HallId,
                HallName = hall.HallName,
                HallCode = hall.HallCode,
                HallCapacity = hall.MaximumCapacity,
                HallRentalPrice = hall.RentalPrice,
                HallDescription = hall.Description ?? string.Empty,
                MenuId = menu.MenuId,
                MenuName = menu.MenuName,
                MenuPricePerTable = menu.PricePerTable,
                EstimatedTableCount = estimatedTableCount,
                MenuTotalPrice = menuTotal,
                MenuDescription = menu.Description ?? string.Empty,
                DecorationPackageId = decor.GoiTrangTriID,
                DecorationPackageName = decor.TenGoi,
                DecorationStyle = decor.PhongCach,
                DecorationPrice = decor.Gia,
                DecorationDescription = decor.MoTa ?? string.Empty,
                DecorationImageUrl = decorCandidate.ImageUrl,
                TotalEstimatedCost = totalCost,
                BudgetDifference = budgetDifference,
                BudgetAnalysisText = budgetDifference >= 0 ? "Trong ngân sách" : "Vượt ngân sách",
                RecommendationReasons = reasons
            });
        }

        var topCombos = scoredCombos
            .OrderByDescending(combo => combo.OverallMatchScore)
            .Take(3)
            .ToList();
        for (var index = 0; index < topCombos.Count; index++)
            topCombos[index].Rank = index + 1;

        return Ok(new RecommendationResponseDto
        {
            RecommendationRequestId = requestEntity.RecommendationRequestId,
            TotalCandidatesEvaluated = scoredCombos.Count,
            TopCombos = topCombos,
            AlgorithmNotice = $"Đã đánh giá {scoredCombos.Count} phương án bằng scoring C# và trả về tối đa 3 kết quả.",
            AnalyzedAt = DateTime.Now
        });
        }
        catch (Exception ex)
        {
            // Bọc toàn bộ flow DB + scoring: log chi tiết, trả 500 có traceId
            // thay vì để UseExceptionHandler che lỗi generic.
            _logger.LogError(ex, "SuggestTop3Combos thất bại (date={Date}, shift={Shift}, guests={Guests}, traceId={TraceId}). Chi tiết: {Detail}",
                targetDate.ToString("yyyy-MM-dd"), desiredShift, request.GuestCount, HttpContext.TraceIdentifier, ex.ToString());
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể phân tích gói tiệc lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    [HttpGet("hall-availability")]
    public async Task<IActionResult> GetHallAvailability([FromQuery] DateTime date)
    {
        DateTime targetDate;
        try
        {
            targetDate = date.Date;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetHallAvailability: date không hợp lệ.");
            return BadRequest(new { message = "Ngày truy vấn không hợp lệ." });
        }
        var nextDay = targetDate.AddDays(1);

        try
        {
        var existingDataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var halls = await _context.Halls.AsNoTracking()
            .Where(hall => hall.DataStatusId == existingDataStatusId &&
                hall.Status != null &&
                hall.Status.StatusGroup == StatusGroups.Hall && hall.Status.StatusCode == HallStatusCodes.Active)
            .OrderBy(hall => hall.HallId)
            .Select(hall => new
            {
                hall.HallId,
                hall.HallName,
                hall.HallCode,
                Capacity = hall.MaximumCapacity,
                hall.RentalPrice
            }).ToListAsync();

        var availableSchedules = await _context.HallSchedules.AsNoTracking()
            .Where(schedule => schedule.Date >= targetDate && schedule.Date < nextDay &&
                schedule.DataStatusId == existingDataStatusId &&
                schedule.Status != null &&
                schedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                schedule.Status.StatusCode == HallScheduleStatusCodes.Available &&
                schedule.Hall != null &&
                schedule.Hall.DataStatusId == existingDataStatusId &&
                schedule.Hall.Status != null &&
                schedule.Hall.Status.StatusGroup == StatusGroups.Hall &&
                schedule.Hall.Status.StatusCode == HallStatusCodes.Active)
            .Select(schedule => new { schedule.HallId, schedule.Shift })
            .ToListAsync();

        return Ok(new HallAvailabilityResponseDto
        {
            QueryDate = targetDate.ToString("yyyy-MM-dd"),
            Halls = halls.Select(hall => new HallAvailabilityItemDto
            {
                HallId = hall.HallId,
                HallName = hall.HallName,
                HallCode = hall.HallCode,
                Capacity = hall.Capacity,
                RentalPrice = hall.RentalPrice,
                IsNoonAvailable = availableSchedules.Any(item => item.HallId == hall.HallId && item.Shift == HallShiftNames.Lunch),
                IsEveningAvailable = availableSchedules.Any(item => item.HallId == hall.HallId && item.Shift == HallShiftNames.Dinner)
            }).ToList()
        });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetHallAvailability thất bại (date={Date}).", targetDate.ToString("yyyy-MM-dd"));
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể tải lịch sảnh lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    [HttpPost("register-booking")]
    public async Task<IActionResult> RegisterPublicBooking([FromBody] RegisterPublicBookingDto? request)
    {
        try
        {
        if (request is null)
            return BadRequest(new { success = false, message = "Thiếu dữ liệu đăng ký giữ chỗ." });
        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { success = false, message = "Vui lòng cung cấp họ tên và số điện thoại liên hệ." });
        if (request.GuestCount <= 0)
            return BadRequest(new { success = false, message = "Số lượng khách phải lớn hơn 0." });

        var existingDataStatus = await _context.DataStatuses.SingleAsync(item => item.DataStatusCode == DataStatusCodes.Existing);
        var cleanPhone = request.PhoneNumber.Trim();
        var customer = await _context.Customers.FirstOrDefaultAsync(customer =>
            customer.DataStatusId == existingDataStatus.DataStatusId &&
            (customer.PhoneNumber == cleanPhone || (request.UserId.HasValue && customer.UserId == request.UserId.Value)));
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerCode = await GenerateCustomerCodeAsync(),
                FullName = request.CustomerName.Trim(),
                PhoneNumber = cleanPhone,
                Email = NormalizeOptional(request.Email),
                UserId = request.UserId,
                CreatedAt = DateTime.Now,
                DataStatusId = existingDataStatus.DataStatusId
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }
        else if (request.UserId.HasValue && !customer.UserId.HasValue)
        {
            customer.UserId = request.UserId.Value;
            await _context.SaveChangesAsync();
        }

        var hasActiveBooking = await _context.WeddingBookings.AnyAsync(booking =>
            booking.CustomerId == customer.CustomerId &&
            BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode));
        if (hasActiveBooking)
            return BadRequest(new { success = false, message = "Khách hàng đã có một booking đang hoạt động." });

        var schedule = await _context.HallSchedules
            .Include(item => item.Hall)
            .Include(item => item.Status)
            .SingleOrDefaultAsync(item => item.HallScheduleId == request.HallScheduleId &&
                item.DataStatusId == existingDataStatus.DataStatusId &&
                item.Hall.DataStatusId == existingDataStatus.DataStatusId);
        if (schedule == null || schedule.Status.StatusGroup != StatusGroups.HallSchedule ||
            schedule.Status.StatusCode != HallScheduleStatusCodes.Available)
            return Conflict(new { success = false, message = "Lịch sảnh không còn AVAILABLE." });
        if (request.GuestCount > schedule.Hall.MaximumCapacity ||
            (schedule.Hall.MinimumCapacity.HasValue && request.GuestCount < schedule.Hall.MinimumCapacity.Value))
            return BadRequest(new { success = false, message = "Số khách không phù hợp sức chứa sảnh." });

        Menu? menu = null;
        if (request.MenuId.HasValue)
        {
            menu = await _context.Menus.SingleOrDefaultAsync(item =>
                item.MenuId == request.MenuId && item.DataStatusId == existingDataStatus.DataStatusId);
            if (menu == null) return BadRequest(new { success = false, message = "Thực đơn không tồn tại." });
        }

        DecorPackage? decor = null;
        if (request.DecorationPackageId.HasValue)
        {
            decor = await _context.DecorPackages.SingleOrDefaultAsync(item =>
                item.GoiTrangTriID == request.DecorationPackageId && item.DataStatusId == existingDataStatus.DataStatusId);
            if (decor == null) return BadRequest(new { success = false, message = "Gói trang trí không tồn tại." });
        }

        var estimatedTableCount = (int)Math.Ceiling(request.GuestCount / 10m);
        var booking = new WeddingBooking
        {
            BookingCode = await GenerateBookingCodeAsync(schedule.Date),
            CustomerId = customer.CustomerId,
            HallScheduleId = schedule.HallScheduleId,
            MenuId = menu?.MenuId,
            DecorationPackageId = decor?.GoiTrangTriID,
            GuestCount = request.GuestCount,
            FinalHallPrice = schedule.Hall.RentalPrice,
            FinalMenuPrice = menu?.PricePerTable,
            FinalDecorationPrice = decor?.Gia,
            EstimatedTotal = schedule.Hall.RentalPrice + (menu?.PricePerTable ?? 0m) * estimatedTableCount + (decor?.Gia ?? 0m),
            SpecialRequests = NormalizeOptional(request.SpecialRequests),
            StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Pending),
            BookedAt = DateTime.Now
        };
        schedule.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked);
        _context.WeddingBookings.Add(booking);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, bookingCode = booking.BookingCode, bookingId = booking.BookingId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RegisterPublicBooking thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể lưu đơn đăng ký lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    private async Task<int?> ResolveAuthenticatedCustomerIdAsync(byte existingDataStatusId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawUserId, out var userId)) return null;
        return await _context.Customers.AsNoTracking()
            .Where(customer => customer.UserId == userId && customer.DataStatusId == existingDataStatusId)
            .Select(customer => (int?)customer.CustomerId)
            .SingleOrDefaultAsync();
    }

    private static double CalculateBudgetScore(decimal totalCost, decimal expectedBudget)
    {
        var ratio = (double)(totalCost / (expectedBudget > 0 ? expectedBudget : 1));
        if (ratio is >= 0.80 and <= 1.00) return 100 - (1.0 - ratio) * 30;
        if (ratio is >= 0.65 and < 0.80) return 85 + (ratio - 0.65) / 0.15 * 10;
        if (ratio is > 1.00 and <= 1.10) return 80 - (ratio - 1.0) / 0.10 * 15;
        return ratio < 0.65 ? 70 : 40;
    }

    private static double CalculateSpaceScore(int guestCount, int maximumCapacity)
    {
        var ratio = (double)guestCount / maximumCapacity;
        if (ratio is >= 0.65 and <= 0.88) return 100;
        if (ratio is > 0.88 and <= 1.00) return 85;
        if (ratio is >= 0.45 and < 0.65) return 80;
        return 55;
    }

    private static double CalculateStyleScore(string? desiredStyle, string? decorStyle)
    {
        var desired = (desiredStyle ?? string.Empty).Trim();
        var decor = (decorStyle ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(desired) || string.IsNullOrEmpty(decor))
            return 70;
        return decor.Contains(desired, StringComparison.OrdinalIgnoreCase) ||
               desired.Contains(decor, StringComparison.OrdinalIgnoreCase)
            ? 85
            : 70;
    }

    private async Task<byte> GetDataStatusIdAsync(string code) => await _context.DataStatuses
        .Where(item => item.DataStatusCode == code).Select(item => item.DataStatusId).SingleAsync();

    private async Task<string> GenerateBookingCodeAsync(DateTime eventDate)
    {
        string code;
        do code = $"DT-{eventDate:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        while (await _context.WeddingBookings.AnyAsync(item => item.BookingCode == code));
        return code;
    }

    private async Task<string> GenerateCustomerCodeAsync()
    {
        string code;
        do code = $"KH-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        while (await _context.Customers.AnyAsync(item => item.CustomerCode == code));
        return code;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // DTO mapping snake_case của Python FastAPI (AI_Service/app/schemas/recommendation.py).
    // Bắt buộc có [JsonPropertyName] + AiJsonOptions case-insensitive để không deserialize ra null.
    private sealed class AiRankItem
    {
        [JsonPropertyName("hall_id")] public int HallId { get; set; }
        [JsonPropertyName("hall_name")] public string HallName { get; set; } = string.Empty;
        [JsonPropertyName("hall_price")] public double HallPrice { get; set; }
        [JsonPropertyName("menu_id")] public int MenuId { get; set; }
        [JsonPropertyName("menu_name")] public string MenuName { get; set; } = string.Empty;
        [JsonPropertyName("price_per_table")] public double PricePerTable { get; set; }
        [JsonPropertyName("total_menu_price")] public double TotalMenuPrice { get; set; }
        [JsonPropertyName("decor_id")] public int DecorId { get; set; }
        [JsonPropertyName("decor_name")] public string DecorName { get; set; } = string.Empty;
        [JsonPropertyName("decor_price")] public double DecorPrice { get; set; }
        [JsonPropertyName("decor_style")] public string DecorStyle { get; set; } = string.Empty;
        [JsonPropertyName("included_services")] public List<string> IncludedServices { get; set; } = new();
        [JsonPropertyName("total_estimated_cost")] public double TotalEstimatedCost { get; set; }
        [JsonPropertyName("matching_score")] public double MatchingScore { get; set; }
        [JsonPropertyName("explanation_arguments")] public List<string> ExplanationArguments { get; set; } = new();
    }

    private sealed class AiRankResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; } = true;
        [JsonPropertyName("total_candidates_evaluated")] public int TotalCandidatesEvaluated { get; set; }
        [JsonPropertyName("top_3")] public List<AiRankItem>? Top3 { get; set; }
        [JsonPropertyName("top_3_recommendations")] public List<AiRankItem>? Top3Recommendations { get; set; }
        [JsonPropertyName("algorithm_notice")] public string? AlgorithmNotice { get; set; }
    }
}
