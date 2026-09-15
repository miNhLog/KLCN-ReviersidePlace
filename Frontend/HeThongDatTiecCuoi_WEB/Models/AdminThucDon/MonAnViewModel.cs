namespace HeThongDatTiecCuoi_WEB.Models.AdminThucDon;

public sealed class MonAnViewModel
{
    public int MonAnID { get; set; }
    public string MaMon { get; set; } = string.Empty;
    public string TenMon { get; set; } = string.Empty;
    public string? NhomMon { get; set; }
    public string? HinhAnh { get; set; }
    public string TrangThai { get; set; } = string.Empty;
}
