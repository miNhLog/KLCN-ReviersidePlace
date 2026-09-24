using System;

namespace HeThongDatTiecCuoi_API.Models;

public class WeddingBooking
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? HallScheduleId { get; set; }
    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }
    public int? ConsultantEmployeeId { get; set; }
    public decimal? ExpectedBudget { get; set; }
    public string? DesiredStyle { get; set; }
    public int? GuestCount { get; set; }
    public int? TableCount { get; set; }
    public decimal? FinalMenuPrice { get; set; }
    public decimal? FinalDecorationPrice { get; set; }
    public decimal? FinalHallPrice { get; set; }
    public decimal? EstimatedTotal { get; set; }
    public string? SpecialRequests { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public DateTime? BookedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    public virtual Customer? Customer { get; set; }
    public virtual HallSchedule? HallSchedule { get; set; }
}