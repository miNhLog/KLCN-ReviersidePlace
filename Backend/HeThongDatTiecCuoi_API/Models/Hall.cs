namespace HeThongDatTiecCuoi_API.Models;

public sealed class Hall
{
    public int HallId { get; set; }
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int? MinimumCapacity { get; set; }
    public int MaximumCapacity { get; set; }
    public decimal RentalPrice { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int StatusId { get; set; }
    public Status Status { get; set; } = null!;
}
