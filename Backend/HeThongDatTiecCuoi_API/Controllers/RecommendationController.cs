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

        // =========================================================================
        // RÀNG BUỘC THỜI GIAN TỔ CHỨC: Tối thiểu 30 ngày, tối đa 540 ngày (18 tháng)
        // =========================================================================
        var minAllowedDate = DateTime.Today.AddDays(30);
        var maxAllowedDate = DateTime.Today.AddDays(365);


        if (request.EventDate.Date < minAllowedDate)
        {
            return BadRequest(new
            {
                success = false,
                message = $"Ngày tổ chức ({request.EventDate:dd/MM/yyyy}) không hợp lệ. Để đảm bảo công tác chuẩn bị sảnh và nguyên liệu ẩm thực chu đáo nhất, Riverside Palace chỉ tiếp nhận tiệc cưới cách ngày hiện tại tối thiểu 30 ngày (từ ngày {minAllowedDate:dd/MM/yyyy} trở đi)."
            });
        }

        if (request.EventDate.Date > maxAllowedDate)
        {
            return BadRequest(new
            {
                success = false,
                message = "Hệ thống chỉ tiếp nhận đặt lịch trong phạm vi 12 tháng tới để bảo đảm tính chuẩn xác của biểu phí dịch vụ."
            });
        }

        // KIỂM TRA NGÀY NGHỈ LỄ TẾT / ĐÓNG CỬA TRUNG TÂM (BLACKOUT DATES)
        if (CheckBlackoutDate(request.EventDate.Date, out string blackoutReason))
        {
            return BadRequest(new
            {
                success = false,
                isBlackout = true,
                message = blackoutReason
            });
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
                    if (decor.TenGoi.Contains("Royal") || decor.TenGoi.Contains("Hoàng gia"))
                    {
                        decorName = "✨ Royal Versailles Gold (Hoàng Gia Cổ Điển)";
                    }
                    else if (decor.TenGoi.Contains("Elegant Blue"))
                    {
                        decorName = "🌊 Midnight Celestial Blue (Đêm Huyền Bí Sắc Xanh)";
                    }
                    else if (decor.TenGoi.Contains("Color of Love") || decor.TenGoi.Contains("Sân vườn"))
                    {
                        decorName = "🌿 Botanical Garden Muse (Vườn Cổ Tích Rustic)";
                    }
                    else if (decor.TenGoi.Contains("Princess") || decor.TenGoi.Contains("Cổ tích"))
                    {
                        decorName = "🌸 Fairy Princesscore (Công Chúa Ngọt Ngào)";
                    }
                    else if (decor.TenGoi.Contains("Endless Love") || decor.TenGoi.Contains("Lãng mạn"))
                    {
                        decorName = "🕊️ Pure Minimalism (Trắng Pha Lê Tinh Giản)";
                    }
                    else if (decor.TenGoi.Contains("ven sông"))
                    {
                        decorName = "🌅 Riverside Sunset Romance (Hoàng Hôn Ven Sông)";
                    }
                    else if (decor.TenGoi.Contains("cuối năm") || decor.TenGoi.Contains("Nhẹ nhàng"))
                    {
                        decorName = "❄️ Cozy Winter Romance (Ấm Áp & Tinh Tế)";
                    }
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

                    // Điểm tổng hợp WSM
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

        var bestOverall = scoredCombos.OrderByDescending(c => c.OverallMatchScore).FirstOrDefault();
        if (bestOverall != null)
        {
            bestOverall.ComboType = "BALANCED";
            bestOverall.ComboTitle = "Cân Bằng Hoàn Hảo";
            bestOverall.ComboBadge = "Khuyên dùng nhất";
            bestOverall.RecommendationReasons.Insert(0, "Phương án tối ưu toàn diện nhất giữa ngân sách, không gian và chất lượng ẩm thực.");
            top3Result.Add(bestOverall);
        }

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

    [HttpGet("hall-availability")]
    public async Task<IActionResult> GetHallAvailability([FromQuery] string? date)
    {
        DateTime targetDate;
        if (string.IsNullOrWhiteSpace(date) || !DateTime.TryParse(date, out targetDate))
        {
            targetDate = DateTime.Today.AddMonths(2);
        }
        targetDate = targetDate.Date;

        // KIỂM TRA RÀNG BUỘC THỜI GIAN TRA CỨU
        var minAllowedDate = DateTime.Today.AddDays(30);
        if (targetDate < minAllowedDate)
        {
            return BadRequest(new
            {
                success = false,
                message = $"Ngày tra cứu ({targetDate:dd/MM/yyyy}) không hợp lệ. Vui lòng chọn ngày cách thời điểm hiện tại tối thiểu 30 ngày."
            });
        }

        // KIỂM TRA NGÀY ĐÓNG CỬA
        if (CheckBlackoutDate(targetDate, out string blackoutReason))
        {
            return BadRequest(new
            {
                success = false,
                isBlackout = true,
                message = blackoutReason
            });
        }

        // Lấy danh sách sảnh đang hoạt động từ DB
        var halls = await _context.Halls
            .Where(h => h.StatusId == 301)
            .OrderBy(h => h.HallId)
            .Select(h => new
            {
                HallId = h.HallId,
                HallName = h.HallName,
                HallCode = h.HallCode,
                Capacity = h.MaximumCapacity,
                RentalPrice = h.RentalPrice
            })
            .ToListAsync();

        // Lấy các ca đã có tiệc vào ngày này (StatusId = 402 hoặc 403)
        var bookedSchedules = await _context.HallSchedules
            .Where(hs => hs.Date == targetDate && (hs.StatusId == 402 || hs.StatusId == 403))
            .Select(hs => new { hs.HallId, hs.Shift })
            .ToListAsync();

        var result = halls.Select(h => new
        {
            hallId = h.HallId,
            hallName = h.HallName,
            hallCode = h.HallCode,
            capacity = h.Capacity,
            rentalPrice = h.RentalPrice,
            isNoonAvailable = !bookedSchedules.Any(s => s.HallId == h.HallId && s.Shift == "Ca trưa"),
            isEveningAvailable = !bookedSchedules.Any(s => s.HallId == h.HallId && s.Shift == "Ca tối")
        }).ToList();

        return Ok(new
        {
            queryDate = targetDate.ToString("yyyy-MM-dd"),
            halls = result
        });
    }

    [HttpPost("register-booking")]
    public async Task<IActionResult> RegisterPublicBooking([FromBody] RegisterPublicBookingDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(new { success = false, message = "Vui lòng cung cấp họ tên và số điện thoại liên hệ." });
            }

            var cleanPhone = request.PhoneNumber.Trim();
            var targetDate = request.EventDate.Date;

            // =========================================================================
            // RÀNG BUỘC THỜI GIAN: Chặn tạo đơn ở quá khứ hoặc dưới 30 ngày
            // =========================================================================
            var minAllowedDate = DateTime.Today.AddDays(30);
            var maxAllowedDate = DateTime.Today.AddDays(540);

            if (targetDate < minAllowedDate)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Ngày tổ chức ({request.EventDate:dd/MM/yyyy}) không hợp lệ. Vui lòng chọn ngày tổ chức cách thời điểm hiện tại tối thiểu 30 ngày."
                });
            }

            if (targetDate > maxAllowedDate)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Hệ thống chỉ tiếp nhận đặt tiệc trong phạm vi 18 tháng tới."
                });
            }

            // CHẶN TẠO ĐƠN GIỮ CHỖ VÀO NGÀY NGHỈ / LỄ TẾT
            if (CheckBlackoutDate(targetDate, out string blackoutReason))
            {
                return BadRequest(new
                {
                    success = false,
                    message = blackoutReason
                });
            }

            // 1. Kiểm tra hoặc tự động tạo mới Khách hàng theo SĐT hoặc UserId
            var customer = await _context.Set<Customer>()
                .FirstOrDefaultAsync(c => c.PhoneNumber == cleanPhone || (request.UserId.HasValue && c.UserId == request.UserId.Value));

            if (customer == null)
            {
                customer = new Customer
                {
                    FullName = request.CustomerName.Trim(),
                    PhoneNumber = cleanPhone,
                    UserId = request.UserId
                };
                _context.Set<Customer>().Add(customer);
                await _context.SaveChangesAsync();
            }
            else
            {
                if (request.UserId.HasValue && !customer.UserId.HasValue)
                {
                    customer.UserId = request.UserId.Value;
                    await _context.SaveChangesAsync();
                }

                // Kiểm tra ràng buộc đơn tiệc đang hoạt động
                var activeBooking = await _context.Set<WeddingBooking>()
                    .Where(b => b.CustomerId == customer.CustomerId && b.Status != "Đã hủy" && b.Status != "Hoàn tất")
                    .OrderByDescending(b => b.BookedAt)
                    .FirstOrDefaultAsync();

                if (activeBooking != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        isDuplicate = true,
                        currentBookingCode = activeBooking.BookingCode,
                        currentStatus = activeBooking.Status,
                        message = $"Hai bạn hiện đã có đơn tiệc [{activeBooking.BookingCode}] đang ở trạng thái '{activeBooking.Status}'. Mỗi khách hàng chỉ giữ chỗ 01 phương án tại một thời điểm. Vui lòng kiểm tra tại mục 'Tiệc của tôi' hoặc hủy đơn cũ trước khi đăng ký gói mới."
                    });
                }
            }

            // 2. Kiểm tra hoặc khởi tạo Lịch sảnh (HallSchedule) an toàn
            var schedule = await _context.HallSchedules
                .FirstOrDefaultAsync(hs => hs.HallId == request.HallId && hs.Date == targetDate && hs.Shift == request.Shift);

            if (schedule == null)
            {
                var validStatusId = await _context.HallSchedules
                    .Select(hs => hs.StatusId)
                    .FirstOrDefaultAsync();
                if (validStatusId == 0) validStatusId = 401;

                schedule = new HallSchedule
                {
                    HallId = request.HallId,
                    Date = targetDate,
                    Shift = request.Shift,
                    StatusId = validStatusId
                };
                _context.HallSchedules.Add(schedule);
                await _context.SaveChangesAsync();
            }
            else if (schedule.StatusId == 402 || schedule.StatusId == 403)
            {
                return BadRequest(new { success = false, message = $"Sảnh này vào {request.Shift} ngày {request.EventDate:dd/MM/yyyy} vừa được khách khác đặt. Vui lòng chọn ca hoặc sảnh khác." });
            }

            // 3. Tính toán đơn giá chi tiết từ CSDL
            var hall = await _context.Halls.FindAsync(request.HallId);
            decimal hallPrice = hall?.RentalPrice ?? 0;

            decimal menuPricePerTable = 0;
            if (request.MenuId.HasValue)
            {
                var menu = await _context.Menus.FindAsync(request.MenuId.Value);
                if (menu != null) menuPricePerTable = menu.PricePerTable;
            }

            decimal decorPrice = 0;
            if (request.DecorationPackageId.HasValue)
            {
                var decor = await _context.DecorPackages.FindAsync(request.DecorationPackageId.Value);
                if (decor != null) decorPrice = decor.Gia;
            }

            decimal totalMenuPrice = menuPricePerTable * request.TableCount;
            decimal estimatedTotal = totalMenuPrice + hallPrice + decorPrice;

            // 4. Lấy nhân viên tư vấn phụ trách hợp lệ
            int? consultantEmployeeId = await _context.Set<WeddingBooking>()
                .Where(b => b.ConsultantEmployeeId.HasValue && b.ConsultantEmployeeId.Value > 0)
                .Select(b => b.ConsultantEmployeeId)
                .FirstOrDefaultAsync();

            if (!consultantEmployeeId.HasValue)
            {
                var rawConn = _context.Database.GetDbConnection();
                bool needToClose = rawConn.State != System.Data.ConnectionState.Open;
                if (needToClose) await rawConn.OpenAsync();

                try
                {
                    using var cmd = rawConn.CreateCommand();
                    cmd.CommandText = "SELECT TOP 1 NhanVienID FROM dbo.NhanVien ORDER BY NhanVienID ASC";
                    var scalar = await cmd.ExecuteScalarAsync();
                    if (scalar != null && scalar != DBNull.Value)
                    {
                        consultantEmployeeId = Convert.ToInt32(scalar);
                    }
                }
                finally
                {
                    if (needToClose) rawConn.Close();
                }
            }

            // 5. Khởi tạo đơn đặt tiệc WeddingBooking
            var bookingCode = $"DT-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            var booking = new WeddingBooking
            {
                BookingCode = bookingCode,
                CustomerId = customer.CustomerId,
                HallScheduleId = schedule.HallScheduleId,
                MenuId = request.MenuId,
                DecorationPackageId = request.DecorationPackageId,
                ConsultantEmployeeId = consultantEmployeeId,
                ExpectedBudget = request.ExpectedBudget,
                DesiredStyle = request.DesiredStyle,
                TableCount = request.TableCount,
                GuestCount = request.TableCount * 10,
                FinalHallPrice = hallPrice,
                FinalMenuPrice = totalMenuPrice,
                FinalDecorationPrice = decorPrice,
                EstimatedTotal = estimatedTotal,
                SpecialRequests = request.SpecialRequests,
                Status = "Chờ xác nhận",
                BookedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Set<WeddingBooking>().Add(booking);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đăng ký giữ chỗ thành công!",
                bookingCode = booking.BookingCode,
                bookingId = booking.BookingId
            });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return BadRequest(new { success = false, message = $"Lỗi CSDL khi tạo đơn: {msg}" });
        }
    }

    // =========================================================================
    // HÀM KIỂM TRA NGÀY ĐÓNG CỬA / NGHỈ LỄ TẾT / BẢO TRÌ (BLACKOUT DATES)
    // =========================================================================
    private bool CheckBlackoutDate(DateTime targetDate, out string reason)
    {
        reason = string.Empty;

        // 1. Đợt nghỉ Tết Nguyên Đán năm 2026 (Từ 28 Tết đến Mùng 6 Tết Bính Ngọ: 15/02/2026 - 22/02/2026)
        var tet2026Start = new DateTime(2026, 2, 15);
        var tet2026End = new DateTime(2026, 2, 22);
        if (targetDate >= tet2026Start && targetDate <= tet2026End)
        {
            reason = $"Ngày {targetDate:dd/MM/yyyy} trùng vào đợt nghỉ Tết Nguyên Đán 2026 của trung tâm (từ 15/02 đến 22/02/2026). Riverside Palace tạm ngưng tiếp nhận tổ chức tiệc cưới trong thời gian này.";
            return true;
        }

        // 2. Đợt nghỉ Tết Nguyên Đán năm 2027 (Từ 28 Tết đến Mùng 6 Tết Đinh Mùi: 04/02/2027 - 12/02/2027)
        var tet2027Start = new DateTime(2027, 2, 4);
        var tet2027End = new DateTime(2027, 2, 12);
        if (targetDate >= tet2027Start && targetDate <= tet2027End)
        {
            reason = $"Ngày {targetDate:dd/MM/yyyy} trùng vào lịch nghỉ Tết Nguyên Đán 2027 của trung tâm (từ 04/02 đến 12/02/2027). Quý khách vui lòng chọn ngày tổ chức sau đợt nghỉ Tết.";
            return true;
        }

        // 3. Đợt đại tu kỹ thuật định kỳ toàn chuỗi trung tâm (Ví dụ thường niên: Ngày 02/09)
        if (targetDate.Month == 9 && targetDate.Day == 2)
        {
            reason = $"Ngày {targetDate:dd/MM/yyyy} là ngày Quốc Khánh, trung tâm dành trọn khuôn viên cho các sự kiện cấp quốc gia và bảo trì kỹ thuật tổng thể.";
            return true;
        }

        return false;
    }

}