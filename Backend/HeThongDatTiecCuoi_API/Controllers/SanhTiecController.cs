using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SanhTiecController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SanhTiecController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/SanhTiec
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var danhSachSanh = await _context.SanhTiec.ToListAsync();

            return Ok(danhSachSanh);
        }

        // GET: api/SanhTiec/1
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var sanh = await _context.SanhTiec.FindAsync(id);

            if (sanh == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy sảnh tiệc"
                });
            }

            return Ok(sanh);
        }

        // POST: api/SanhTiec
        [HttpPost]
        public async Task<IActionResult> Create(SanhTiec sanhTiec)
        {
            _context.SanhTiec.Add(sanhTiec);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = sanhTiec.SanhTiecID },
                sanhTiec
            );
        }
    }
}