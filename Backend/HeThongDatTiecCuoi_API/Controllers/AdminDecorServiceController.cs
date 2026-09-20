using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;

namespace HeThongDatTiecCuoi_API.Controllers
{
    [Route("api/admin/decor-service")]
    [ApiController]
    [Authorize(Roles = "Quản trị viên,Nhân viên tư vấn")]
    public class AdminDecorServiceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminDecorServiceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- DECOR PACKAGES ---
        [HttpGet("decor")]
        public async Task<IActionResult> GetDecorList()
        {
            var list = await _context.DecorPackages.ToListAsync();
            return Ok(new { success = true, data = list });
        }

        [HttpPost("decor")]
        public async Task<IActionResult> CreateDecor([FromBody] DecorPackage model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _context.DecorPackages.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm gói decor thành công!" });
        }

        [HttpPut("decor/{id}")]
        public async Task<IActionResult> UpdateDecor(int id, [FromBody] DecorPackage model)
        {
            var decor = await _context.DecorPackages.FindAsync(id);
            if (decor == null) return NotFound(new { success = false, message = "Không tìm thấy gói decor." });

            decor.TenGoi = model.TenGoi;
            decor.PhongCach = model.PhongCach;
            decor.MoTa = model.MoTa;
            decor.Gia = model.Gia;
            decor.TrangThai = model.TrangThai;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật gói decor thành công!" });
        }

        // --- SERVICES ---
        [HttpGet("service")]
        public async Task<IActionResult> GetServiceList()
        {
            var list = await _context.ServiceItems.ToListAsync();
            return Ok(new { success = true, data = list });
        }

        [HttpPost("service")]
        public async Task<IActionResult> CreateService([FromBody] ServiceItem model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _context.ServiceItems.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm dịch vụ thành công!" });
        }

        [HttpPut("service/{id}")]
        public async Task<IActionResult> UpdateService(int id, [FromBody] ServiceItem model)
        {
            var service = await _context.ServiceItems.FindAsync(id);
            if (service == null) return NotFound(new { success = false, message = "Không tìm thấy dịch vụ." });

            service.TenDichVu = model.TenDichVu;
            service.LoaiDichVu = model.LoaiDichVu;
            service.MoTa = model.MoTa;
            service.Gia = model.Gia;
            service.TrangThai = model.TrangThai;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật dịch vụ thành công!" });
        }
    }
}