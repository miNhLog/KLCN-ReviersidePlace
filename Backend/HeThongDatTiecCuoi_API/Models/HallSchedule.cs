namespace HeThongDatTiecCuoi_API.Models;

public sealed class HallSchedule
{
    public int HallScheduleId { get; set; }

    public int HallId { get; set; }

    public DateTime Date { get; set; }

    public string Shift { get; set; } = string.Empty;

    public string Status { get; set; } = "Trống";

    public string? Notes { get; set; }

    public Hall Hall { get; set; } = null!;
}
