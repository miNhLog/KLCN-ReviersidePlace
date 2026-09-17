namespace HeThongDatTiecCuoi_API.Models;

public sealed class HallSchedule
{
    public int HallScheduleId { get; set; }

    public int HallId { get; set; }

    public DateTime Date { get; set; }

    public string Shift { get; set; } = string.Empty;

    public int StatusId { get; set; }

    public string? Notes { get; set; }

    public Hall Hall { get; set; } = null!;
    public Status Status { get; set; } = null!;
}
