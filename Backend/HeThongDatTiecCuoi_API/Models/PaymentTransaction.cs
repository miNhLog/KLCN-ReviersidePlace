namespace HeThongDatTiecCuoi_API.Models;

public sealed class PaymentTransaction
{
    public long GiaoDichId { get; set; }
    public int DatTiecId { get; set; }
    public long OrderCode { get; set; }
    public decimal SoTienGiaoDich { get; set; }
    public decimal SoTienGhiNhan { get; set; }
    public string PhuongThuc { get; set; } = "VIETQR_PAYOS";
    public string TrangThai { get; set; } = "PENDING";
    public DateTime NgayTao { get; set; } = DateTime.Now;
    public DateTime? NgayThanhToan { get; set; }
    public string? PayloadWebhook { get; set; }

    public WeddingBooking Booking { get; set; } = null!;
}
