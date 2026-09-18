using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongDatTiecCuoi_API.Models
{
    [Table("GoiTrangTri")]
    public class DecorPackage
    {
        [Key]
        public int GoiTrangTriID { get; set; }
        [Required]
        public string MaGoi { get; set; } = string.Empty;
        [Required]
        public string TenGoi { get; set; } = string.Empty;
        public string? PhongCach { get; set; }
        public string? MoTa { get; set; }
        public decimal Gia { get; set; }
        public string? HinhAnh { get; set; }
        public string TrangThai { get; set; } = "Áp dụng";
    }
}