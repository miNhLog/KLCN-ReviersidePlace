using System;
using System.Collections.Generic;

namespace HeThongDatTiecCuoi_API.DTOs.Booking;

public class MyBookingDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    // Thông tin sảnh & ca
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;

    // Chi tiết quy mô & gói
    public int TableCount { get; set; }
    public int GuestCount { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public decimal MenuPricePerTable { get; set; }
    public string DecorName { get; set; } = string.Empty;
    public decimal DecorPrice { get; set; }

    // Chi phí & Trạng thái
    public decimal EstimatedTotal { get; set; }
    public string Status { get; set; } = "Chờ xác nhận";
    public DateTime BookedAt { get; set; }
    public string? SpecialRequests { get; set; }

    // Chuyên viên tư vấn phụ trách
    public string? ConsultantName { get; set; }
    public string? ConsultantPhone { get; set; }
}