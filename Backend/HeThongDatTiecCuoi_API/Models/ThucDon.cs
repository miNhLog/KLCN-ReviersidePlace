namespace HeThongDatTiecCuoi_API.Models;

public sealed class ThucDon
{
    public int ThucDonID { get; set; }

    public string MaThucDon { get; set; } = string.Empty;

    public string TenThucDon { get; set; } = string.Empty;

    public string? MoTa { get; set; }

    public decimal GiaMoiBan { get; set; }

    public string TrangThai { get; set; } = string.Empty;


    // Một thực đơn có nhiều món ăn
    public ICollection<ChiTietThucDon> ChiTietThucDons { get; set; }
        = new List<ChiTietThucDon>();
}