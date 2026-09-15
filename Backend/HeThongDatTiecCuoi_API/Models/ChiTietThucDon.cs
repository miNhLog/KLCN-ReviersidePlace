namespace HeThongDatTiecCuoi_API.Models;

public sealed class ChiTietThucDon
{
    public int ChiTietThucDonID { get; set; }

    public int ThucDonID { get; set; }

    public int MonAnID { get; set; }

    public int SoThuTu { get; set; }


    // Navigation
    public ThucDon ThucDon { get; set; } = null!;

    public MonAn MonAn { get; set; } = null!;
}