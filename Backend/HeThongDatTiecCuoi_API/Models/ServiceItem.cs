using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongDatTiecCuoi_API.Models
{
    [Table("DichVu")]
    public class ServiceItem
    {
        [Key]
        public int DichVuID { get; set; }
        [Required]
        public string MaDichVu { get; set; } = string.Empty;
        [Required]
        public string TenDichVu { get; set; } = string.Empty;
        public string? LoaiDichVu { get; set; }
        public string? MoTa { get; set; }
        public decimal Gia { get; set; }
        public string? HinhAnh { get; set; }
        public string TrangThai { get; set; } = "Áp dụng";
    }
}