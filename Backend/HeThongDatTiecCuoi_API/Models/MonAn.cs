namespace HeThongDatTiecCuoi_API.Models;

public sealed class MonAn
{
    public int MonAnID { get; set; }

    public string MaMon { get; set; } = string.Empty;

    public string TenMon { get; set; } = string.Empty;

    public string? NhomMon { get; set; }

    public string? HinhAnh { get; set; }

    public string TrangThai { get; set; } = string.Empty;


    // Một món ăn có thể thuộc nhiều thực đơn
    public ICollection<ChiTietThucDon> ChiTietThucDons { get; set; }
        = new List<ChiTietThucDon>();
}