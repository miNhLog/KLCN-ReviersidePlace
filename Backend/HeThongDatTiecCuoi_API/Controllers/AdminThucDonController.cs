using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AdminThucDon;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Quản trị viên")]
public sealed class AdminThucDonController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminThucDonController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetDanhSachThucDon(
        [FromQuery] string? tuKhoa,
        [FromQuery] string? trangThai,
        CancellationToken cancellationToken)
    {
        var query = _context.ThucDon
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var tuKhoaTimKiem = tuKhoa.Trim();

            query = query.Where(x =>
                x.MaThucDon.Contains(tuKhoaTimKiem) ||
                x.TenThucDon.Contains(tuKhoaTimKiem));
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            var trangThaiTimKiem = trangThai.Trim();

            query = query.Where(x =>
                x.TrangThai == trangThaiTimKiem);
        }

        var danhSach = await query
            .OrderBy(x => x.MaThucDon)
            .Select(x => new ThucDonDto
            {
                ThucDonID = x.ThucDonID,
                MaThucDon = x.MaThucDon,
                TenThucDon = x.TenThucDon,
                MoTa = x.MoTa,
                GiaMoiBan = x.GiaMoiBan,
                TrangThai = x.TrangThai
            })
            .ToListAsync(cancellationToken);

        return Ok(danhSach);
    }
    [HttpPost]
    public async Task<IActionResult> TaoThucDon(
    [FromBody] TaoThucDonRequest request,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenThucDon))
        {
            return BadRequest(new
            {
                message = "Tên thực đơn không được để trống."
            });
        }

        if (request.GiaMoiBan < 0)
        {
            return BadRequest(new
            {
                message = "Giá mỗi bàn không hợp lệ."
            });
        }

        var danhSachMa = await _context.ThucDon
            .AsNoTracking()
            .Select(x => x.MaThucDon)
            .ToListAsync(cancellationToken);

        var soLonNhat = danhSachMa
            .Where(x =>
                x.StartsWith("TD") &&
                int.TryParse(x.Substring(2), out _))
            .Select(x => int.Parse(x.Substring(2)))
            .DefaultIfEmpty(0)
            .Max();

        var maThucDonMoi = $"TD{soLonNhat + 1:D3}";

        var thucDon = new Models.ThucDon
        {
            MaThucDon = maThucDonMoi,
            TenThucDon = request.TenThucDon.Trim(),
            MoTa = string.IsNullOrWhiteSpace(request.MoTa)
                ? null
                : request.MoTa.Trim(),
            GiaMoiBan = request.GiaMoiBan,
            TrangThai = "Áp dụng"
        };

        _context.ThucDon.Add(thucDon);

        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetDanhSachThucDon),
            new { id = thucDon.ThucDonID },
            new ThucDonDto
            {
                ThucDonID = thucDon.ThucDonID,
                MaThucDon = thucDon.MaThucDon,
                TenThucDon = thucDon.TenThucDon,
                MoTa = thucDon.MoTa,
                GiaMoiBan = thucDon.GiaMoiBan,
                TrangThai = thucDon.TrangThai
            });
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> CapNhatThucDon(
    int id,
    [FromBody] CapNhatThucDonRequest request,
    CancellationToken cancellationToken)
    {
        var thucDon = await _context.ThucDon
            .FirstOrDefaultAsync(x => x.ThucDonID == id, cancellationToken);

        if (thucDon == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy thực đơn."
            });
        }

        if (string.IsNullOrWhiteSpace(request.TenThucDon))
        {
            return BadRequest(new
            {
                message = "Tên thực đơn không được để trống."
            });
        }

        if (request.GiaMoiBan < 0)
        {
            return BadRequest(new
            {
                message = "Giá mỗi bàn không hợp lệ."
            });
        }

        thucDon.TenThucDon = request.TenThucDon.Trim();

        thucDon.MoTa = string.IsNullOrWhiteSpace(request.MoTa)
            ? null
            : request.MoTa.Trim();

        thucDon.GiaMoiBan = request.GiaMoiBan;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new ThucDonDto
        {
            ThucDonID = thucDon.ThucDonID,
            MaThucDon = thucDon.MaThucDon,
            TenThucDon = thucDon.TenThucDon,
            MoTa = thucDon.MoTa,
            GiaMoiBan = thucDon.GiaMoiBan,
            TrangThai = thucDon.TrangThai
        });
    }
    [HttpPatch("{id:int}/trang-thai")]
    public async Task<IActionResult> CapNhatTrangThaiThucDon(
    int id,
    [FromBody] CapNhatTrangThaiThucDonRequest request,
    CancellationToken cancellationToken)
    {
        var thucDon = await _context.ThucDon
            .FirstOrDefaultAsync(x => x.ThucDonID == id, cancellationToken);

        if (thucDon == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy thực đơn."
            });
        }

        var cacTrangThaiHopLe = new[]
        {
        "Áp dụng",
        "Ngừng áp dụng"
    };

        if (string.IsNullOrWhiteSpace(request.TrangThai) ||
            !cacTrangThaiHopLe.Contains(request.TrangThai.Trim()))
        {
            return BadRequest(new
            {
                message = "Trạng thái thực đơn không hợp lệ."
            });
        }

        thucDon.TrangThai = request.TrangThai.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            thucDon.ThucDonID,
            thucDon.MaThucDon,
            thucDon.TenThucDon,
            thucDon.TrangThai
        });
    }
    [HttpGet("{id:int}/mon-an")]
    public async Task<IActionResult> GetMonAnTrongThucDon(
    int id,
    CancellationToken cancellationToken)
    {
        var thucDonTonTai = await _context.ThucDon
            .AsNoTracking()
            .AnyAsync(x => x.ThucDonID == id, cancellationToken);

        if (!thucDonTonTai)
        {
            return NotFound(new
            {
                message = "Không tìm thấy thực đơn."
            });
        }

        var danhSachMonAn = await _context.ChiTietThucDon
            .AsNoTracking()
            .Where(x => x.ThucDonID == id)
            .OrderBy(x => x.SoThuTu)
            .Select(x => new ChiTietThucDonDto
            {
                ChiTietThucDonID = x.ChiTietThucDonID,
                MonAnID = x.MonAnID,
                MaMon = x.MonAn.MaMon,
                TenMon = x.MonAn.TenMon,
                NhomMon = x.MonAn.NhomMon,
                HinhAnh = x.MonAn.HinhAnh,
                TrangThai = x.MonAn.TrangThai,
                SoThuTu = x.SoThuTu
            })
            .ToListAsync(cancellationToken);

        return Ok(danhSachMonAn);
    }
    [HttpPost("{id:int}/mon-an")]
    public async Task<IActionResult> ThemMonVaoThucDon(
    int id,
    [FromBody] ThemMonVaoThucDonRequest request,
    CancellationToken cancellationToken)
    {
        // 1. Kiểm tra thực đơn tồn tại
        var thucDonTonTai = await _context.ThucDon
            .AsNoTracking()
            .AnyAsync(x => x.ThucDonID == id, cancellationToken);

        if (!thucDonTonTai)
        {
            return NotFound(new
            {
                message = "Không tìm thấy thực đơn."
            });
        }

        // Không cho thay đổi thành phần nếu thực đơn đã được dùng trong đặt tiệc
        var soDatTiecDangDung = await _context.Database
            .SqlQuery<int>($"""
        SELECT COUNT(*) AS [Value]
        FROM DatTiec
        WHERE ThucDonID = {id}
        """)
            .SingleAsync(cancellationToken);

        if (soDatTiecDangDung > 0)
        {
            return BadRequest(new
            {
                message = "Thực đơn đã được sử dụng trong đặt tiệc nên không thể thay đổi thành phần món."
            });
        }

        // 2. Kiểm tra món ăn tồn tại
        var monAn = await _context.MonAn
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.MonAnID == request.MonAnID,
                cancellationToken);

        if (monAn == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy món ăn."
            });
        }

        // 3. Chỉ được thêm món đang phục vụ
        if (monAn.TrangThai != "Đang phục vụ")
        {
            return BadRequest(new
            {
                message = "Chỉ có thể thêm món đang phục vụ vào thực đơn."
            });
        }

        // 4. Kiểm tra món đã có trong thực đơn chưa
        var monDaTonTai = await _context.ChiTietThucDon
            .AsNoTracking()
            .AnyAsync(
                x => x.ThucDonID == id &&
                     x.MonAnID == request.MonAnID,
                cancellationToken);

        if (monDaTonTai)
        {
            return BadRequest(new
            {
                message = "Món ăn này đã có trong thực đơn."
            });
        }

        // 5. Tính vị trí cuối cùng
        var soThuTuLonNhat = await _context.ChiTietThucDon
            .Where(x => x.ThucDonID == id)
            .Select(x => (int?)x.SoThuTu)
            .MaxAsync(cancellationToken) ?? 0;

        var chiTiet = new Models.ChiTietThucDon
        {
            ThucDonID = id,
            MonAnID = request.MonAnID,
            SoThuTu = soThuTuLonNhat + 1
        };

        _context.ChiTietThucDon.Add(chiTiet);

        await _context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/AdminThucDon/{id}/mon-an",
            new ChiTietThucDonDto
            {
                ChiTietThucDonID = chiTiet.ChiTietThucDonID,
                MonAnID = monAn.MonAnID,
                MaMon = monAn.MaMon,
                TenMon = monAn.TenMon,
                NhomMon = monAn.NhomMon,
                HinhAnh = monAn.HinhAnh,
                TrangThai = monAn.TrangThai,
                SoThuTu = chiTiet.SoThuTu
            });
    }
    [HttpDelete("{id:int}/mon-an/{monAnId:int}")]
    public async Task<IActionResult> XoaMonKhoiThucDon(
    int id,
    int monAnId,
    CancellationToken cancellationToken)
    {
        // 1. Kiểm tra thực đơn tồn tại
        var thucDonTonTai = await _context.ThucDon
            .AsNoTracking()
            .AnyAsync(x => x.ThucDonID == id, cancellationToken);

        if (!thucDonTonTai)
        {
            return NotFound(new
            {
                message = "Không tìm thấy thực đơn."
            });
        }

        // 2. Không cho thay đổi nếu thực đơn đã được dùng trong đặt tiệc
        var soDatTiecDangDung = await _context.Database
            .SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value]
            FROM DatTiec
            WHERE ThucDonID = {id}
            """)
            .SingleAsync(cancellationToken);

        if (soDatTiecDangDung > 0)
        {
            return BadRequest(new
            {
                message = "Thực đơn đã được sử dụng trong đặt tiệc nên không thể thay đổi thành phần món."
            });
        }

        // 3. Tìm món trong thực đơn
        var chiTiet = await _context.ChiTietThucDon
            .FirstOrDefaultAsync(
                x => x.ThucDonID == id &&
                     x.MonAnID == monAnId,
                cancellationToken);

        if (chiTiet == null)
        {
            return NotFound(new
            {
                message = "Món ăn không tồn tại trong thực đơn này."
            });
        }

        var soThuTuBiXoa = chiTiet.SoThuTu;

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        // 4. Xóa liên kết món - thực đơn
        _context.ChiTietThucDon.Remove(chiTiet);

        await _context.SaveChangesAsync(cancellationToken);

        // 5. Dồn lại số thứ tự phía sau
        await _context.ChiTietThucDon
            .Where(x =>
                x.ThucDonID == id &&
                x.SoThuTu > soThuTuBiXoa)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.SoThuTu,
                    x => x.SoThuTu - 1),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            message = "Bỏ món khỏi thực đơn thành công.",
            thucDonID = id,
            monAnID = monAnId
        });
    }
    [HttpPut("{id:int}/mon-an/sap-xep")]
    public async Task<IActionResult> SapXepMonAn(
    int id,
    [FromBody] SapXepMonAnRequest request,
    CancellationToken cancellationToken)
    {
        // 1. Kiểm tra thực đơn tồn tại
        var thucDonTonTai = await _context.ThucDon
            .AsNoTracking()
            .AnyAsync(x => x.ThucDonID == id, cancellationToken);

        if (!thucDonTonTai)
        {
            return NotFound(new
            {
                message = "Không tìm thấy thực đơn."
            });
        }

        // 2. Không cho thay đổi nếu thực đơn đã được dùng trong đặt tiệc
        var soDatTiecDangDung = await _context.Database
            .SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value]
            FROM DatTiec
            WHERE ThucDonID = {id}
            """)
            .SingleAsync(cancellationToken);

        if (soDatTiecDangDung > 0)
        {
            return BadRequest(new
            {
                message = "Thực đơn đã được sử dụng trong đặt tiệc nên không thể thay đổi thành phần món."
            });
        }

        // 3. Kiểm tra danh sách gửi lên
        if (request.DanhSachMonAnID == null ||
            request.DanhSachMonAnID.Count == 0)
        {
            return BadRequest(new
            {
                message = "Danh sách món ăn không được để trống."
            });
        }

        // Không cho MonAnID bị lặp
        if (request.DanhSachMonAnID.Distinct().Count()
            != request.DanhSachMonAnID.Count)
        {
            return BadRequest(new
            {
                message = "Danh sách món ăn có dữ liệu bị trùng."
            });
        }

        // 4. Lấy các món hiện tại của thực đơn
        var danhSachHienTai = await _context.ChiTietThucDon
            .AsNoTracking()
            .Where(x => x.ThucDonID == id)
            .Select(x => x.MonAnID)
            .ToListAsync(cancellationToken);

        // Số lượng phải giống nhau
        if (danhSachHienTai.Count != request.DanhSachMonAnID.Count)
        {
            return BadRequest(new
            {
                message = "Danh sách món ăn không khớp với thực đơn hiện tại."
            });
        }

        // Tất cả MonAnID gửi lên phải thật sự thuộc thực đơn
        var hopLe = request.DanhSachMonAnID
            .All(monAnId => danhSachHienTai.Contains(monAnId));

        if (!hopLe)
        {
            return BadRequest(new
            {
                message = "Danh sách chứa món ăn không thuộc thực đơn."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // 5. Đưa SoThuTu sang giá trị tạm âm
            // để tránh đụng unique (ThucDonID, SoThuTu)
            await _context.ChiTietThucDon
                .Where(x => x.ThucDonID == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.SoThuTu,
                        x => -x.ChiTietThucDonID),
                    cancellationToken);

            // 6. Load lại dữ liệu sau khi đã đổi sang số tạm
            var cacChiTiet = await _context.ChiTietThucDon
                .Where(x => x.ThucDonID == id)
                .ToListAsync(cancellationToken);

            // 7. Gán thứ tự mới 1, 2, 3...
            for (int i = 0; i < request.DanhSachMonAnID.Count; i++)
            {
                var monAnId = request.DanhSachMonAnID[i];

                var chiTiet = cacChiTiet
                    .First(x => x.MonAnID == monAnId);

                chiTiet.SoThuTu = i + 1;
            }

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Ok(new
            {
                message = "Sắp xếp món ăn trong thực đơn thành công."
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}