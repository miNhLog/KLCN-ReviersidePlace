using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Booking;

// 1. DTO bộ lọc danh sách tiệc cưới
public class BookingFilterRequestDto
{
    public string? Keyword { get; set; } // Tìm theo mã tiệc, tên khách, số điện thoại
    public string? Status { get; set; }  // 'Chờ xác nhận', 'Đã xác nhận', 'Đã cọc', 'Hoàn tất', 'Đã hủy'
    public int? HallId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

// 2. DTO danh sách đơn tiệc trả về kèm phân trang & thống kê nhanh
public class BookingListResponseDto
{
    public List<BookingSummaryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Thống kê nhanh theo trạng thái
    public int PendingConfirmationCount { get; set; } // Chờ xác nhận
    public int ConfirmedCount { get; set; }           // Đã xác nhận
    public int DepositedCount { get; set; }           // Đã cọc
    public int CompletedCount { get; set; }           // Hoàn tất
    public int CancelledCount { get; set; }           // Đã hủy
}

// 3. DTO thông tin rút gọn hiển thị trên bảng
public class BookingSummaryDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty; // 'Ca trưa' hoặc 'Ca tối'
    public int TableCount { get; set; }
    public decimal EstimatedTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? BookedAt { get; set; }
}

// 4. DTO xem chi tiết đơn đặt tiệc & lập hợp đồng
public class BookingDetailDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;

    // Khách hàng
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    // Sảnh tiệc & Ngày ca
    public int? HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string HallCode { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;

    // Thực đơn & Trang trí
    public int? MenuId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public int? DecorationPackageId { get; set; }
    public string DecorationPackageName { get; set; } = string.Empty;

    // Quy mô tiệc
    public int GuestCount { get; set; }
    public int TableCount { get; set; }
    public decimal ExpectedBudget { get; set; }
    public string DesiredStyle { get; set; } = string.Empty;
    public string SpecialRequests { get; set; } = string.Empty;

    // Chi phí chốt
    public decimal FinalMenuPrice { get; set; }
    public decimal FinalDecorationPrice { get; set; }
    public decimal FinalHallPrice { get; set; }
    public decimal EstimatedTotal { get; set; }

    // Trạng thái & Ghi chú
    public string Status { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public DateTime? BookedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// 5. DTO tạo mới đơn đặt tiệc
public class CreateBookingRequestDto
{
    [Required(ErrorMessage = "Khách hàng không được để trống")]
    public int CustomerId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn sảnh tiệc")]
    public int HallId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày tổ chức")]
    public DateTime EventDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ca tổ chức (Ca trưa hoặc Ca tối)")]
    public string Shift { get; set; } = "Ca tối";

    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }

    [Range(10, 3000, ErrorMessage = "Số lượng khách từ 10 đến 3000")]
    public int GuestCount { get; set; }

    [Range(1, 300, ErrorMessage = "Số lượng bàn từ 1 đến 300")]
    public int TableCount { get; set; }

    public decimal? ExpectedBudget { get; set; }
    public string? DesiredStyle { get; set; }
    public string? SpecialRequests { get; set; }
}

// 6. DTO cập nhật trạng thái đơn đặt tiệc (Duyệt, Đặt cọc, Hủy)
public class UpdateBookingStatusRequestDto
{
    [Required]
    public string NewStatus { get; set; } = string.Empty; // 'Đã xác nhận', 'Đã cọc', 'Hoàn tất', 'Đã hủy'
    public string? CancellationReason { get; set; } // Nếu hủy
    public decimal? DepositAmount { get; set; }       // Số tiền cọc nếu chuyển sang 'Đã cọc'
    public string? Notes { get; set; }
}