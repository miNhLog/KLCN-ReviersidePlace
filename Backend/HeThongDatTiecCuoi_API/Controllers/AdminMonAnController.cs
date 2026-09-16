using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AdminMonAn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Quản trị viên")]
public sealed class AdminMonAnController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public AdminMonAnController(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetDanhSachMonAn(
        [FromQuery] string? tuKhoa,
        [FromQuery] string? nhomMon,
        [FromQuery] string? trangThai,
        CancellationToken cancellationToken)
    {
        var query = _context.MonAn
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var tuKhoaTimKiem = tuKhoa.Trim();

            query = query.Where(x =>
                x.MaMon.Contains(tuKhoaTimKiem) ||
                x.TenMon.Contains(tuKhoaTimKiem));
        }

        if (!string.IsNullOrWhiteSpace(nhomMon))
        {
            var nhomMonTimKiem = nhomMon.Trim();

            query = query.Where(x =>
                x.NhomMon == nhomMonTimKiem);
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            var trangThaiTimKiem = trangThai.Trim();

            query = query.Where(x =>
                x.TrangThai == trangThaiTimKiem);
        }

        var danhSach = await query
            .OrderBy(x => x.MaMon)
            .Select(x => new MonAnDto
            {
                MonAnID = x.MonAnID,
                MaMon = x.MaMon,
                TenMon = x.TenMon,
                NhomMon = x.NhomMon,
                HinhAnh = x.HinhAnh,
                TrangThai = x.TrangThai
            })
            .ToListAsync(cancellationToken);

        return Ok(danhSach);
    }
    [HttpPost]
    public async Task<IActionResult> TaoMonAn(
    [FromBody] TaoMonAnRequest request,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenMon))
        {
            return BadRequest(new
            {
                message = "Tên món không được để trống."
            });
        }

        var cacNhomMonHopLe = new[]
        {
        "Món khai vị",
        "Món súp",
        "Món chính",
        "Món tráng miệng"
    };

        if (string.IsNullOrWhiteSpace(request.NhomMon) ||
            !cacNhomMonHopLe.Contains(request.NhomMon.Trim()))
        {
            return BadRequest(new
            {
                message = "Nhóm món không hợp lệ."
            });
        }

        var danhSachMa = await _context.MonAn
            .AsNoTracking()
            .Select(x => x.MaMon)
            .ToListAsync(cancellationToken);

        var soLonNhat = danhSachMa
            .Where(x =>
                x.StartsWith("MA") &&
                int.TryParse(x.Substring(2), out _))
            .Select(x => int.Parse(x.Substring(2)))
            .DefaultIfEmpty(0)
            .Max();

        var maMonMoi = $"MA{soLonNhat + 1:D3}";

        var monAn = new Models.MonAn
        {
            MaMon = maMonMoi,
            TenMon = request.TenMon.Trim(),
            NhomMon = request.NhomMon.Trim(),
            HinhAnh = null,
            TrangThai = "Đang phục vụ"
        };

        _context.MonAn.Add(monAn);

        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetDanhSachMonAn),
            new { id = monAn.MonAnID },
            new MonAnDto
            {
                MonAnID = monAn.MonAnID,
                MaMon = monAn.MaMon,
                TenMon = monAn.TenMon,
                NhomMon = monAn.NhomMon,
                HinhAnh = monAn.HinhAnh,
                TrangThai = monAn.TrangThai
            });
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> CapNhatMonAn(
    int id,
    [FromBody] CapNhatMonAnRequest request,
    CancellationToken cancellationToken)
    {
        var monAn = await _context.MonAn
            .FirstOrDefaultAsync(x => x.MonAnID == id, cancellationToken);

        if (monAn == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy món ăn."
            });
        }

        if (string.IsNullOrWhiteSpace(request.TenMon))
        {
            return BadRequest(new
            {
                message = "Tên món không được để trống."
            });
        }

        var cacNhomMonHopLe = new[]
        {
        "Món khai vị",
        "Món súp",
        "Món chính",
        "Món tráng miệng"
    };

        if (string.IsNullOrWhiteSpace(request.NhomMon) ||
            !cacNhomMonHopLe.Contains(request.NhomMon.Trim()))
        {
            return BadRequest(new
            {
                message = "Nhóm món không hợp lệ."
            });
        }

        monAn.TenMon = request.TenMon.Trim();
        monAn.NhomMon = request.NhomMon.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new MonAnDto
        {
            MonAnID = monAn.MonAnID,
            MaMon = monAn.MaMon,
            TenMon = monAn.TenMon,
            NhomMon = monAn.NhomMon,
            HinhAnh = monAn.HinhAnh,
            TrangThai = monAn.TrangThai
        });
    }
    [HttpPatch("{id:int}/trang-thai")]
    public async Task<IActionResult> CapNhatTrangThaiMonAn(
    int id,
    [FromBody] CapNhatTrangThaiMonAnRequest request,
    CancellationToken cancellationToken)
    {
        var monAn = await _context.MonAn
            .FirstOrDefaultAsync(x => x.MonAnID == id, cancellationToken);

        if (monAn == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy món ăn."
            });
        }

        var cacTrangThaiHopLe = new[]
        {
        "Đang phục vụ",
        "Ngừng phục vụ"
    };

        if (string.IsNullOrWhiteSpace(request.TrangThai) ||
            !cacTrangThaiHopLe.Contains(request.TrangThai.Trim()))
        {
            return BadRequest(new
            {
                message = "Trạng thái món ăn không hợp lệ."
            });
        }

        monAn.TrangThai = request.TrangThai.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            monAn.MonAnID,
            monAn.MaMon,
            monAn.TenMon,
            monAn.TrangThai
        });
    }

    [HttpPost("{id:int}/hinh-anh")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadHinhAnh(
    int id,
    [FromForm] IFormFile file,
    CancellationToken cancellationToken)
    {
        var monAn = await _context.MonAn
            .FirstOrDefaultAsync(x => x.MonAnID == id, cancellationToken);

        if (monAn == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy món ăn."
            });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Vui lòng chọn ảnh."
            });
        }

        // Giới hạn 5 MB
        const long kichThuocToiDa = 5 * 1024 * 1024;

        if (file.Length > kichThuocToiDa)
        {
            return BadRequest(new
            {
                message = "Ảnh không được vượt quá 5 MB."
            });
        }

        var duoiFile = Path.GetExtension(file.FileName).ToLowerInvariant();

        var cacDuoiFileChoPhep = new[]
        {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

        if (!cacDuoiFileChoPhep.Contains(duoiFile))
        {
            return BadRequest(new
            {
                message = "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP."
            });
        }

        var thuMucAnh = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "mon-an"
        );

        Directory.CreateDirectory(thuMucAnh);

        var tenFileMoi = $"{Guid.NewGuid():N}{duoiFile}";

        var duongDanFile = Path.Combine(
            thuMucAnh,
            tenFileMoi
        );

        await using (var stream = new FileStream(
            duongDanFile,
            FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        // Nếu món đã có ảnh cũ thì xóa ảnh cũ
        if (!string.IsNullOrWhiteSpace(monAn.HinhAnh))
        {
            var tenAnhCu = Path.GetFileName(monAn.HinhAnh);

            var duongDanAnhCu = Path.Combine(
                thuMucAnh,
                tenAnhCu
            );

            if (System.IO.File.Exists(duongDanAnhCu))
            {
                System.IO.File.Delete(duongDanAnhCu);
            }
        }

        monAn.HinhAnh = $"/uploads/mon-an/{tenFileMoi}";

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new MonAnDto
        {
            MonAnID = monAn.MonAnID,
            MaMon = monAn.MaMon,
            TenMon = monAn.TenMon,
            NhomMon = monAn.NhomMon,
            HinhAnh = monAn.HinhAnh,
            TrangThai = monAn.TrangThai
        });
    }
    [HttpDelete("{id:int}/hinh-anh")]
    public async Task<IActionResult> XoaHinhAnh(
    int id,
    CancellationToken cancellationToken)
    {
        var monAn = await _context.MonAn
            .FirstOrDefaultAsync(x => x.MonAnID == id, cancellationToken);

        if (monAn == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy món ăn."
            });
        }

        if (string.IsNullOrWhiteSpace(monAn.HinhAnh))
        {
            return BadRequest(new
            {
                message = "Món ăn hiện chưa có hình ảnh."
            });
        }

        var tenFile = Path.GetFileName(monAn.HinhAnh);

        var duongDanFile = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "mon-an",
            tenFile
        );

        if (System.IO.File.Exists(duongDanFile))
        {
            System.IO.File.Delete(duongDanFile);
        }

        monAn.HinhAnh = null;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Xóa hình ảnh món ăn thành công.",
            monAn.MonAnID,
            monAn.MaMon,
            monAn.TenMon,
            HinhAnh = monAn.HinhAnh
        });
    }
}