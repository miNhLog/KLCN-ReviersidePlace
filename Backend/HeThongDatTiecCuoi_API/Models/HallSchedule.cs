namespace HeThongDatTiecCuoi_API.Models;

using HeThongDatTiecCuoi_API.Constants.StatusCodes;

public sealed class HallSchedule
{
    public int HallScheduleId { get; set; }

    public int HallId { get; set; }

    public DateTime Date { get; set; }

    public string Shift { get; set; } = string.Empty;

    public string Status { get; set; } = HallScheduleStatusCodes.Available;

    public string? Notes { get; set; }

    public Hall Hall { get; set; } = null!;
}
