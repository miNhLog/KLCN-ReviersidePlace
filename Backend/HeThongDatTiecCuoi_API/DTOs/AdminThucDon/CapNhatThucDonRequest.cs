namespace HeThongDatTiecCuoi_API.DTOs.AdminThucDon;

public sealed class CapNhatThucDonRequest
{
    public string TenThucDon { get; set; } = string.Empty;

    public string? MoTa { get; set; }

    public decimal GiaMoiBan { get; set; }
}