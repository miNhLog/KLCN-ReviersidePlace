namespace HeThongDatTiecCuoi_WEB.Models.AdminThucDon;

public sealed class QuanLyThucDonViewModel
{
    public List<ThucDonViewModel> DanhSachThucDon { get; set; } = new();
    public List<MonAnViewModel> DanhSachMonAn { get; set; } = new();

    // Dùng cho modal "Thêm món vào thực đơn".
    public List<MonAnViewModel> DanhSachMonDangPhucVu { get; set; } = new();

    public int? ThucDonDangChonID { get; set; }
    public List<ChiTietThucDonViewModel> DanhSachMonTrongThucDon { get; set; } = new();

    public string? TuKhoaThucDon { get; set; }
    public string? TrangThaiThucDon { get; set; }

    public string? TuKhoaMonAn { get; set; }
    public string? NhomMon { get; set; }
    public string? TrangThaiMonAn { get; set; }
}
