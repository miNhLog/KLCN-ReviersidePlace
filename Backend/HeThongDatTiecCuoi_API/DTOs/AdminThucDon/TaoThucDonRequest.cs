namespace HeThongDatTiecCuoi_API.DTOs.AdminThucDon;

public sealed class TaoThucDonRequest
{
    public string TenThucDon { get; set; } = string.Empty;

    public string? MoTa { get; set; }

    public decimal GiaMoiBan { get; set; }
}