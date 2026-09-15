namespace HeThongDatTiecCuoi_API.DTOs.AdminMonAn;

public sealed class MonAnDto
{
    public int MonAnID { get; set; }

    public string MaMon { get; set; } = string.Empty;

    public string TenMon { get; set; } = string.Empty;

    public string? NhomMon { get; set; }

    public string? HinhAnh { get; set; }

    public string TrangThai { get; set; } = string.Empty;
}