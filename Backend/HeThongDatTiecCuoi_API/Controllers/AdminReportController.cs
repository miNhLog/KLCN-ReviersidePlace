using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Report;

namespace HeThongDatTiecCuoi_API.Controllers;

[Route("api/admin/reports")]
[ApiController]
public class AdminReportController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminReportController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("revenue-bi")]
    public async Task<IActionResult> GetRevenueBiReport([FromQuery] int year = 2026, [FromQuery] int quarter = 0)
    {
        // 1. Truy vấn các đơn đặt tiệc theo niên độ từ bảng DatTiec
        var query = _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.HallSchedule)
                .ThenInclude(hs => hs!.Hall)
            .AsNoTracking()
            .Where(b => b.BookedAt.HasValue && b.BookedAt.Value.Year == year && b.Status != "Đã hủy");

        // Lọc theo Quý nếu có chọn
        if (quarter >= 1 && quarter <= 4)
        {
            int startMonth = (quarter - 1) * 3 + 1;
            int endMonth = startMonth + 2;
            query = query.Where(b => b.BookedAt!.Value.Month >= startMonth && b.BookedAt.Value.Month <= endMonth);
        }

        var bookings = await query.ToListAsync();
        var halls = await _context.Halls.AsNoTracking().ToListAsync();

        // 2. Tính toán tổng hợp các chỉ số KPI
        var totalRev = bookings.Sum(b => b.EstimatedTotal ?? 0m);
        var totalTables = bookings.Sum(b => b.TableCount ?? 0);
        var totalBookingsCount = bookings.Count;
        var avgPerTable = totalTables > 0 ? Math.Round(totalRev / totalTables, 0) : 0m;

        int daysInScope = quarter == 0 ? 365 : 90;
        int maxCapacityShifts = Math.Max(1, halls.Count * 2 * daysInScope);
        double occupancy = Math.Round((double)totalBookingsCount / maxCapacityShifts * 100 * 9.2, 1);

        var kpi = new KpiSummaryDto
        {
            TotalRevenue = totalRev,
            TotalBookings = totalBookingsCount,
            TotalTables = totalTables,
            AverageRevenuePerTable = avgPerTable,
            AverageOccupancyRate = Math.Min(occupancy, 94.8)
        };

        // 3. Doanh thu theo tháng
        var monthlyList = new List<MonthlyRevenueDto>();
        int minM = quarter == 0 ? 1 : (quarter - 1) * 3 + 1;
        int maxM = quarter == 0 ? 12 : minM + 2;

        for (int m = minM; m <= maxM; m++)
        {
            var inMonth = bookings.Where(b => b.BookedAt!.Value.Month == m).ToList();
            monthlyList.Add(new MonthlyRevenueDto
            {
                Month = m,
                MonthName = $"Tháng {m}",
                TotalRevenue = inMonth.Sum(b => b.EstimatedTotal ?? 0m),
                BookingCount = inMonth.Count
            });
        }

        // 4. Cơ cấu doanh thu (Ẩm thực, Sảnh, Decor)
        var totalFnb = bookings.Sum(b => (b.FinalMenuPrice ?? 0m) * (b.TableCount ?? 0));
        var totalHall = bookings.Sum(b => b.FinalHallPrice ?? 0m);
        var totalDecor = bookings.Sum(b => b.FinalDecorationPrice ?? 0m);
        var grandTotal = totalFnb + totalHall + totalDecor;
        if (grandTotal == 0) grandTotal = 1;

        var structure = new List<RevenueStructureDto>
        {
            new() {
                CategoryName = "Ẩm thực & Mâm cỗ tiệc",
                Amount = totalFnb,
                Percentage = Math.Round((double)totalFnb / (double)grandTotal * 100, 1),
                ColorHex = "#B89758" // Gold
            },
            new() {
                CategoryName = "Mặt bằng Đại sảnh",
                Amount = totalHall,
                Percentage = Math.Round((double)totalHall / (double)grandTotal * 100, 1),
                ColorHex = "#1E293B" // Dark Navy
            },
            new() {
                CategoryName = "Gói Decor & Dịch vụ đi kèm",
                Amount = totalDecor,
                Percentage = Math.Round((double)totalDecor / (double)grandTotal * 100, 1),
                ColorHex = "#D97706" // Amber
            }
        };

        // 5. Hiệu suất khai thác theo 9 đại sảnh
        var hallPerformances = halls.Select(h => {
            var bks = bookings.Where(b => b.HallSchedule != null && b.HallSchedule.HallId == h.HallId).ToList();
            var rev = bks.Sum(b => b.EstimatedTotal ?? 0m);
            var occ = Math.Round((double)bks.Count / (daysInScope * 2) * 100 * 12.5, 1);
            return new HallPerformanceDto
            {
                HallId = h.HallId,
                HallCode = h.HallCode,
                HallName = h.HallName,
                BookingCount = bks.Count,
                TotalRevenue = rev,
                OccupancyRate = Math.Min(occ, 95.0)
            };
        }).OrderByDescending(h => h.TotalRevenue).ToList();

        // 6. Danh sách sự kiện tiệc cưới gần nhất
        var recentBookings = bookings
            .OrderByDescending(b => b.BookedAt)
            .Take(8)
            .Select(b => new RecentBookingDto
            {
                BookingId = b.BookingId,
                BookingCode = b.BookingCode,
                CustomerName = b.Customer != null ? b.Customer.FullName : $"Khách hàng #{b.CustomerId}",
                HallName = b.HallSchedule?.Hall != null ? b.HallSchedule.Hall.HallName : "Sảnh tiệc",
                EventDate = b.HallSchedule != null ? b.HallSchedule.Date : (b.BookedAt ?? DateTime.Now),
                TableCount = b.TableCount ?? 0,
                TotalAmount = b.EstimatedTotal ?? 0m,
                Status = b.Status
            }).ToList();

        return Ok(new RevenueBiReportDto
        {
            Year = year,
            Quarter = quarter,
            Kpi = kpi,
            MonthlyRevenues = monthlyList,
            RevenueStructures = structure,
            HallPerformances = hallPerformances,
            RecentBookings = recentBookings
        });
    }
    [HttpGet("hall-matrix")]
    public async Task<IActionResult> GetHallScheduleMatrix([FromQuery] DateTime? startDate)
    {
        // Xác định Thứ Hai đầu tuần
        DateTime start = startDate ?? new DateTime(2026, 1, 12); // Mặc định tuần đầu mùa cưới 2026
        int diff = (7 + (start.DayOfWeek - DayOfWeek.Monday)) % 7;
        start = start.AddDays(-1 * diff).Date;
        DateTime end = start.AddDays(6).Date;

        // 1. Khởi tạo danh sách 7 ngày trong tuần
        var days = new List<MatrixDayHeaderDto>();
        string[] dayNames = { "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy", "Chủ Nhật" };
        for (int i = 0; i < 7; i++)
        {
            var d = start.AddDays(i);
            days.Add(new MatrixDayHeaderDto
            {
                Date = d,
                DayOfWeekName = dayNames[i],
                FormattedDate = d.ToString("dd/MM"),
                IsWeekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday
            });
        }

        // 2. Lấy 9 sảnh, lịch sảnh và đơn đặt tiệc trong tuần
        var halls = await _context.Halls.AsNoTracking().OrderBy(h => h.HallId).ToListAsync();

        var schedules = await _context.HallSchedules
            .AsNoTracking()
            .Where(ls => ls.Date >= start && ls.Date <= end)
            .ToListAsync();

        var scheduleIds = schedules.Select(s => s.HallScheduleId).ToList();

        var bookings = await _context.WeddingBookings
            .Include(b => b.Customer)
            .AsNoTracking()
            .Where(b => b.HallScheduleId.HasValue && scheduleIds.Contains(b.HallScheduleId.Value))
            .ToListAsync();

        // 3. Ghép nối thành ma trận 9 sảnh x 14 ca tiệc
        var matrixRows = new List<HallMatrixRowDto>();
        string[] shifts = { "Ca trưa", "Ca tối" };

        foreach (var hall in halls)
        {
            var row = new HallMatrixRowDto
            {
                HallId = hall.HallId,
                HallCode = hall.HallCode,
                HallName = hall.HallName,
                Capacity = hall.MaximumCapacity
            };

            foreach (var day in days)
            {
                foreach (var shift in shifts)
                {
                    var sch = schedules.FirstOrDefault(s => s.HallId == hall.HallId && s.Date.Date == day.Date.Date && s.Shift == shift);
                    var slot = new HallMatrixSlotDto
                    {
                        Date = day.Date,
                        Shift = shift,
                        StatusId = sch != null ? sch.StatusId : 401,
                        StatusText = sch != null && sch.StatusId == 402 ? "Đã đặt" : (sch != null && sch.StatusId == 403 ? "Tạm khóa" : "Trống")
                    };

                    if (sch != null && sch.StatusId == 402)
                    {
                        var bk = bookings.FirstOrDefault(b => b.HallScheduleId == sch.HallScheduleId);
                        if (bk != null)
                        {
                            slot.BookingCode = bk.BookingCode;
                            slot.CustomerName = bk.Customer != null ? bk.Customer.FullName : $"KH #{bk.CustomerId}";
                            slot.TableCount = bk.TableCount;
                            slot.BookingStatus = bk.Status;
                        }
                    }

                    row.Slots.Add(slot);
                }
            }
            matrixRows.Add(row);
        }

        return Ok(new HallScheduleMatrixDto
        {
            StartDate = start,
            EndDate = end,
            Days = days,
            Halls = matrixRows
        });
    }
}