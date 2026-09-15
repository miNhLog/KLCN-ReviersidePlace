using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HeThongDatTiecCuoi_WEB.Models.AdminThucDon;

public sealed class TaoThucDonForm
{
    [Required]
    public string TenThucDon { get; set; } = string.Empty;

    public string? MoTa { get; set; }

    [Range(0, double.MaxValue)]
    public decimal GiaMoiBan { get; set; }
}

public sealed class CapNhatThucDonForm
{
    public int ThucDonID { get; set; }

    [Required]
    public string TenThucDon { get; set; } = string.Empty;

    public string? MoTa { get; set; }

    [Range(0, double.MaxValue)]
    public decimal GiaMoiBan { get; set; }
}

public sealed class TaoMonAnForm
{
    [Required]
    public string TenMon { get; set; } = string.Empty;

    [Required]
    public string NhomMon { get; set; } = string.Empty;

    public IFormFile? HinhAnh { get; set; }
}

public sealed class CapNhatMonAnForm
{
    public int MonAnID { get; set; }

    [Required]
    public string TenMon { get; set; } = string.Empty;

    [Required]
    public string NhomMon { get; set; } = string.Empty;

    public IFormFile? HinhAnhMoi { get; set; }

    public bool XoaHinhAnh { get; set; }
}
