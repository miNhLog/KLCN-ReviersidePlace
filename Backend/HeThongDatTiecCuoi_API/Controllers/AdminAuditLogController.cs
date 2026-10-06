using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AuditLog;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminAuditLogController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminAuditLogController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<AuditLogPageDto>> GetAuditLogs(
        int page = 1,
        int pageSize = 20,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        action = string.IsNullOrWhiteSpace(action)
            ? null
            : action.Trim().ToUpperInvariant();
        keyword = string.IsNullOrWhiteSpace(keyword)
            ? null
            : keyword.Trim();
        fromDate = fromDate?.Date;
        toDate = toDate?.Date;

        if (fromDate.HasValue && toDate.HasValue && toDate < fromDate)
        {
            return BadRequest(new { message = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu." });
        }

        if (keyword?.Length > 200)
        {
            return BadRequest(new { message = "Từ khóa tìm kiếm không được vượt quá 200 ký tự." });
        }

        var query =
            from log in _context.AuditLogs.AsNoTracking()
            join actorUserRecord in _context.Users.AsNoTracking() on log.UserId equals actorUserRecord.UserId into actorUsers
            from actorUser in actorUsers.DefaultIfEmpty()
            join actorEmployeeRecord in _context.Employees.AsNoTracking() on log.UserId equals actorEmployeeRecord.UserId into actorEmployees
            from actorEmployee in actorEmployees.DefaultIfEmpty()
            join targetUserRecord in _context.Users.AsNoTracking() on log.EntityId equals (long)targetUserRecord.UserId into targetUsers
            from targetUser in targetUsers.DefaultIfEmpty()
            join targetEmployeeRecord in _context.Employees.AsNoTracking() on log.EntityId equals (long)targetEmployeeRecord.UserId into targetEmployees
            from targetEmployee in targetEmployees.DefaultIfEmpty()
            where log.EntityName == AuditEntityNames.User && AuditActions.AccountActions.Contains(log.Action)
            select new { log, actorUser, actorEmployee, targetUser, targetEmployee };

        if (action is not null)
        {
            if (!AuditActions.AccountActions.Contains(action))
            {
                return BadRequest(new { message = "Loại thao tác không hợp lệ." });
            }
            query = query.Where(item => item.log.Action == action);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(item => item.log.Timestamp >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            var endExclusive = toDate.Value.AddDays(1);
            query = query.Where(item => item.log.Timestamp < endExclusive);
        }

        if (keyword is not null)
        {
            query = query.Where(item =>
                (item.actorUser != null && item.actorUser.Email.Contains(keyword)) ||
                (item.actorEmployee != null && item.actorEmployee.FullName.Contains(keyword)) ||
                (item.targetUser != null && item.targetUser.Email.Contains(keyword)) ||
                (item.targetEmployee != null &&
                    (item.targetEmployee.EmployeeCode.Contains(keyword) || item.targetEmployee.FullName.Contains(keyword))) ||
                (item.log.Notes != null && item.log.Notes.Contains(keyword)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
        }

        var items = await query
            .OrderByDescending(item => item.log.Timestamp)
            .ThenByDescending(item => item.log.AuditLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AuditLogDto
            {
                AuditLogId = item.log.AuditLogId,
                Timestamp = item.log.Timestamp,
                ActorUserId = item.log.UserId,
                Actor = item.actorEmployee != null
                    ? item.actorEmployee.FullName + " (" + item.actorUser!.Email + ")"
                    : item.actorUser != null ? item.actorUser.Email : "Hệ thống",
                Action = item.log.Action,
                TargetUserId = item.log.EntityId,
                Target = item.targetEmployee != null
                    ? item.targetEmployee.EmployeeCode + " - " + item.targetEmployee.FullName
                    : item.targetUser != null ? item.targetUser.Email : "Tài khoản #" + item.log.EntityId,
                OldData = item.log.OldData,
                NewData = item.log.NewData,
                Notes = item.log.Notes
            })
            .ToListAsync(cancellationToken);

        return Ok(new AuditLogPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        });
    }
}
