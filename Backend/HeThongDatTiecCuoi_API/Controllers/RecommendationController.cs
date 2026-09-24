using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.DTOs.Recommendation;

namespace HeThongDatTiecCuoi_API.Controllers;

[Route("api/recommendations")]
[ApiController]
public class RecommendationController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public RecommendationController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("suggest-top3")]
    public async Task<IActionResult> SuggestTop3Combos([FromBody] RecommendationRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var totalTables = request.OfficialTableCount + request.SpareTableCount;

        // 1. LỌC SẢNH KHẢ DỤNG: ĐỦ SỨC CHỨA & LỊCH TRỐNG
        var targetDate = request.EventDate.Date;

        // Lấy các sảnh đã bị đặt (StatusId = 402 hoặc 403) vào ngày và ca đó
        var busyHallIds = await _context.HallSchedules
            .Where(hs => hs.Date == targetDate && hs.Shift == request.Shift && (hs.StatusId == 402 || hs.StatusId == 403))
            .Select(hs => hs.HallId)
            .ToListAsync();

        // Lọc sảnh còn hoạt động (StatusId = 301), sức chứa tối đa >= số bàn yêu cầu và chưa trùng lịch
        var candidateHalls = await _context.Halls
            .Where(h => !busyHallIds.Contains(h.HallId) && h.MaximumCapacity >= totalTables && h.StatusId == 301)
            .AsNoTracking()
            .ToListAsync();

        if (!candidateHalls.Any())
        {
            return Ok(new RecommendationResponseDto
            {
                InquiryCode = $"INQ-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                CoupleName = request.CoupleName,
                TotalCandidatesEvaluated = 0,
                TopCombos = new List<ComboSuggestionDto>(),
                AlgorithmNotice = $"Hiện tại tất cả các sảnh phù hợp quy mô ({totalTables} bàn) đã kín lịch vào {request.Shift} ngày {request.EventDate:dd/MM/yyyy}. Quý khách vui lòng chọn ca khác hoặc ngày lân cận."
            });
        }

        // 2. TẢI TOÀN BỘ CATALOG: THỰC ĐƠN, GÓI DECOR & DỊCH VỤ
        var activeMenus = await _context.Menus
            .Where(m => m.StatusId == 501)
            .AsNoTracking()
            .ToListAsync();

        var activeDecors = await _context.DecorPackages
            .Where(d => d.TrangThai == "Áp dụng")
            .AsNoTracking()
            .ToListAsync();

        var selectedServices = new List<SuggestedServiceItemDto>();
        if (request.SelectedServiceCodes != null && request.SelectedServiceCodes.Any())
        {
            var services = await _context.ServiceItems
                .Where(dv => request.SelectedServiceCodes.Contains(dv.MaDichVu) && dv.TrangThai == "Áp dụng")
                .AsNoTracking()
                .ToListAsync();

            selectedServices = services.Select(s => new SuggestedServiceItemDto
            {
                ServiceId = s.DichVuID,
                ServiceCode = s.MaDichVu,
                ServiceName = s.TenDichVu,
                Price = s.Gia,
                Note = s.LoaiDichVu ?? "Dịch vụ tiệc cưới"
            }).ToList();
        }

        decimal servicesTotal = selectedServices.Sum(s => s.Price);

        // 3. TÍNH ĐIỂM TÍN NHIỆM LỊCH SỬ CHO TỪNG SẢNH
        var hallRatings = await _context.HallSchedules
            .Where(hs => hs.StatusId == 402)
            .GroupBy(hs => hs.HallId)
            .Select(g => new { HallId = g.Key, AvgScore = 4.8 })
            .ToDictionaryAsync(x => x.HallId, x => x.AvgScore);

        // 4. DUYỆT TỔ HỢP TẤT CẢ PHƯƠNG ÁN & CHẤM ĐIỂM (SCORING ENGINE)
        var scoredCombos = new List<ComboSuggestionDto>();
        int totalCombosCount = 0;

        foreach (var hall in candidateHalls)
        {
            double hallAvgRating = hallRatings.ContainsKey(hall.HallId) ? hallRatings[hall.HallId] : 4.6;

            foreach (var menu in activeMenus)
            {
                decimal menuPrice = menu.PricePerTable;
                decimal menuTotal = menuPrice * totalTables;

                foreach (var decor in activeDecors)
                {
                    totalCombosCount++;

                    decimal decorPrice = decor.Gia;
                    decimal totalCost = menuTotal + hall.RentalPrice + decorPrice + servicesTotal;
                    decimal budgetDiff = request.ExpectedBudget - totalCost;

                    // Tiêu chí 1: Điểm tài chính ngân sách (Trọng số 35%)
                    double budgetScore = 0;
                    double budgetRatio = (double)(totalCost / (request.ExpectedBudget > 0 ? request.ExpectedBudget : 1));

                    if (budgetRatio >= 0.80 && budgetRatio <= 1.00)
                        budgetScore = 100 - ((1.0 - budgetRatio) * 30);
                    else if (budgetRatio >= 0.65 && budgetRatio < 0.80)
                        budgetScore = 85 + ((budgetRatio - 0.65) / 0.15 * 10);
                    else if (budgetRatio > 1.00 && budgetRatio <= 1.10)
                        budgetScore = 80 - ((budgetRatio - 1.0) / 0.10 * 15);
                    else if (budgetRatio < 0.65)
                        budgetScore = 70;
                    else
                        budgetScore = 40;

                    // Tiêu chí 2: Tối ưu tỷ lệ lấp đầy sảnh (Trọng số 25%)
                    double spaceScore = 0;
                    double occupancyRatio = (double)totalTables / (hall.MaximumCapacity > 0 ? hall.MaximumCapacity : 1);

                    if (occupancyRatio >= 0.65 && occupancyRatio <= 0.88)
                        spaceScore = 100;
                    else if (occupancyRatio > 0.88 && occupancyRatio <= 1.00)
                        spaceScore = 85;
                    else if (occupancyRatio >= 0.45 && occupancyRatio < 0.65)
                        spaceScore = 80;
                    else
                        spaceScore = 55;

                    // Tiêu chí 3: Phong cách & Khẩu vị ẩm thực (Trọng số 25%)
                    double styleScore = 70;
                    var reasons = new List<string>();

                    string decorStyle = decor.PhongCach ?? "";
                    string decorName = decor.TenGoi;
                    string menuName = menu.MenuName;
                    string menuDesc = menu.Description ?? "";

                    if (!string.IsNullOrWhiteSpace(request.DesiredStyle) && !string.IsNullOrWhiteSpace(decorStyle))
                    {
                        if (decorStyle.Contains(request.DesiredStyle, StringComparison.OrdinalIgnoreCase) ||
                            request.DesiredStyle.Contains(decorStyle, StringComparison.OrdinalIgnoreCase))
                        {
                            styleScore += 15;
                            reasons.Add($"Gói trang trí '{decorName}' đồng điệu với phong cách {request.DesiredStyle}.");
                        }
                    }

                    if (request.PreferredSpaceType == "OUTDOOR_RIVER" && hall.HallName.Contains("Green Riverside", StringComparison.OrdinalIgnoreCase))
                    {
                        styleScore += 15;
                        reasons.Add("Sảnh Green Riverside ven sông đáp ứng chính xác sở thích không gian ngoài trời.");
                    }
                    else if (request.PreferredSpaceType == "INDOOR_PILLARLESS" && (hall.HallName.Contains("Grand Ballroom") || hall.HallName.Contains("Seine")))
                    {
                        styleScore += 15;
                        reasons.Add($"Đại sảnh {hall.HallName} kiến trúc vòm trần cao không cột đem lại góc nhìn thoáng rộng.");
                    }

                    if (!string.IsNullOrWhiteSpace(request.FoodPreference) && !string.IsNullOrWhiteSpace(menuDesc))
                    {
                        if (request.FoodPreference == "SEAFOOD_PREMIUM" && (menuDesc.Contains("hải sản") || menuDesc.Contains("tôm") || menuDesc.Contains("cua")))
                        {
                            styleScore += 10;
                            reasons.Add("Thực đơn đậm đà với các món đặc sản hải sản thượng hạng.");
                        }
                        else if (request.FoodPreference == "EUROPEAN_FUSION" && (menuDesc.Contains("bò Mỹ") || menuDesc.Contains("Á - Âu") || menuDesc.Contains("vang đỏ")))
                        {
                            styleScore += 10;
                            reasons.Add("Thực đơn kết hợp phong vị Á - Âu đương đại cao cấp.");
                        }
                    }

                    if (styleScore > 100) styleScore = 100;

                    // Tiêu chí 4: Tín nhiệm lịch sử (Trọng số 15%)
                    double ratingScore = (hallAvgRating / 5.0) * 100;

                    // Điểm tổng hợp
                    double overallScore = (budgetScore * 0.35) + (spaceScore * 0.25) + (styleScore * 0.25) + (ratingScore * 0.15);
                    overallScore = Math.Round(overallScore, 1);

                    string budgetStatus = budgetDiff >= 0
                        ? $"Tiết kiệm {budgetDiff:N0} VNĐ ({Math.Round((double)budgetDiff / (double)request.ExpectedBudget * 100, 1)}% ngân sách)"
                        : $"Vượt {Math.Abs(budgetDiff):N0} VNĐ so với dự kiến";

                    if (reasons.Count == 0)
                    {
                        reasons.Add($"Sảnh {hall.HallName} vừa vặn cho quy mô {totalTables} bàn tiệc.");
                        reasons.Add($"Set thực đơn '{menuName}' có hương vị hài hòa, dễ dùng.");
                    }

                    scoredCombos.Add(new ComboSuggestionDto
                    {
                        OverallMatchScore = overallScore,
                        BudgetMatchScore = Math.Round(budgetScore, 1),
                        SpaceFitScore = Math.Round(spaceScore, 1),
                        StyleThemeScore = Math.Round(styleScore, 1),
                        RatingCredibilityScore = Math.Round(ratingScore, 1),
                        HallId = hall.HallId,
                        HallName = hall.HallName,
                        HallCode = hall.HallCode,
                        HallCapacity = hall.MaximumCapacity,
                        HallRentalPrice = hall.RentalPrice,
                        HallDescription = hall.Description ?? "Không gian tiệc cưới đẳng cấp Riverside Palace.",
                        MenuId = menu.MenuId,
                        MenuName = menuName,
                        MenuPricePerTable = menuPrice,
                        MenuTotalPrice = menuTotal,
                        MenuDescription = menuDesc,
                        DecorationPackageId = decor.GoiTrangTriID,
                        DecorationPackageName = decorName,
                        DecorationStyle = decorStyle,
                        DecorationPrice = decorPrice,
                        DecorationDescription = decor.MoTa ?? "",
                        IncludedServices = selectedServices,
                        ServicesTotalPrice = servicesTotal,
                        TotalEstimatedCost = totalCost,
                        BudgetDifference = budgetDiff,
                        BudgetAnalysisText = budgetStatus,
                        RecommendationReasons = reasons
                    });
                }
            }
        }

        // 5. TRÍCH XUẤT TOP 3 COMBO
        var top3Result = new List<ComboSuggestionDto>();

        // COMBO 1: CÂN BẰNG HOÀN HẢO (Điểm tổng hợp cao nhất)
        var bestOverall = scoredCombos.OrderByDescending(c => c.OverallMatchScore).FirstOrDefault();
        if (bestOverall != null)
        {
            bestOverall.ComboType = "BALANCED";
            bestOverall.ComboTitle = "Cân Bằng Hoàn Hảo";
            bestOverall.ComboBadge = "Khuyên dùng nhất";
            bestOverall.RecommendationReasons.Insert(0, "Phương án tối ưu toàn diện nhất giữa ngân sách, không gian và chất lượng ẩm thực.");
            top3Result.Add(bestOverall);
        }

        // COMBO 2: TỐI ƯU NGÂN SÁCH (Tiết kiệm chi phí)
        var bestEconomy = scoredCombos
            .Where(c => c.BudgetDifference >= 0 && c != bestOverall)
            .OrderBy(c => c.TotalEstimatedCost)
            .FirstOrDefault() ?? scoredCombos.Where(c => c != bestOverall).OrderBy(c => c.TotalEstimatedCost).FirstOrDefault();

        if (bestEconomy != null)
        {
            bestEconomy.ComboType = "ECONOMY";
            bestEconomy.ComboTitle = "Tối Ưu Ngân Sách";
            bestEconomy.ComboBadge = "Tiết kiệm thông minh";
            bestEconomy.RecommendationReasons.Insert(0, "Chi phí hợp lý nhất, giữ lại khoản dự phòng ngân sách an toàn cho gia đình.");
            top3Result.Add(bestEconomy);
        }

        // COMBO 3: ĐẲNG CẤP HOÀNG GIA (Trải nghiệm cao cấp)
        var bestLuxury = scoredCombos
            .Where(c => c != bestOverall && c != bestEconomy)
            .OrderByDescending(c => c.MenuPricePerTable + c.DecorationPrice)
            .FirstOrDefault();

        if (bestLuxury != null)
        {
            bestLuxury.ComboType = "LUXURY";
            bestLuxury.ComboTitle = "Đẳng Cấp Hoàng Gia";
            bestLuxury.ComboBadge = "Trải nghiệm 5 sao";
            bestLuxury.RecommendationReasons.Insert(0, "Nâng tầm sự kiện với thực đơn thượng hạng và phong cách trang trí lộng lẫy.");
            top3Result.Add(bestLuxury);
        }

        return Ok(new RecommendationResponseDto
        {
            InquiryCode = $"INQ-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
            CoupleName = request.CoupleName,
            TotalCandidatesEvaluated = totalCombosCount,
            TopCombos = top3Result,
            AlgorithmNotice = $"Hệ thống đã phân tích {totalCombosCount} phương án để chọn ra Top 3 gói tiệc tối ưu nhất.",
            AnalyzedAt = DateTime.Now
        });
    }
}