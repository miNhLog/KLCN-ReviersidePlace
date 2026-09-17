using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AdminAccount;
using HeThongDatTiecCuoi_API.DTOs.Common;
using HeThongDatTiecCuoi_API.Helpers;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Services;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/admin/accounts")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminAccountController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IStatusService _statusService;
    private readonly IPasswordResetService _passwordResetService;

    public AdminAccountController(
        ApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        IStatusService statusService,
        IPasswordResetService passwordResetService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _statusService = statusService;
        _passwordResetService = passwordResetService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAccounts(
        string? keyword,
        int? roleId,
        string? status,
        CancellationToken cancellationToken)
    {
        var query =
            from user in _context.Users.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on user.RoleId equals role.RoleId
            join employeeRecord in _context.Employees.AsNoTracking()
                on user.UserId equals employeeRecord.UserId
                into employeeGroup
            from employee in employeeGroup.DefaultIfEmpty()
            join customerRecord in _context.Customers.AsNoTracking()
                on user.UserId equals customerRecord.UserId
                into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            select new
            {
                user,
                role,
                employee,
                customer
            };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();

            query = query.Where(item =>
                item.user.Email.Contains(keyword) ||
                (item.employee != null &&
                 ((item.employee.FullName ?? "").Contains(keyword) ||
                  (item.employee.EmployeeCode ?? "").Contains(keyword) ||
                  (item.employee.PhoneNumber ?? "").Contains(keyword))) ||
                (item.customer != null &&
                 ((item.customer.FullName ?? "").Contains(keyword) ||
                  (item.customer.PhoneNumber ?? "").Contains(keyword))));
        }

        if (roleId.HasValue)
        {
            query = query.Where(item => item.user.RoleId == roleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.user.Status.StatusCode == status);
        }

        var accounts = await query
            .OrderByDescending(item => item.user.CreatedAt)
            .Select(item => new AccountDto
            {
                UserId = item.user.UserId,
                Email = item.user.Email,
                RoleId = item.user.RoleId,
                RoleName = item.role.RoleName,
                FullName = item.employee != null
                    ? item.employee.FullName
                    : item.customer != null
                        ? item.customer.FullName
                        : null,
                EmployeeCode = item.employee != null
                    ? item.employee.EmployeeCode
                    : null,
                PhoneNumber = item.employee != null
                    ? item.employee.PhoneNumber
                    : item.customer != null
                        ? item.customer.PhoneNumber
                        : null,
                Status = item.user.Status.StatusCode,
                StatusName = item.user.Status.StatusName,
                CreatedAt = item.user.CreatedAt,
                EmployeeStatus = item.employee != null
                    ? item.employee.Status.StatusCode
                    : null,
                EmployeeStatusName = item.employee != null
                    ? item.employee.Status.StatusName
                    : null
            })
            .ToListAsync(cancellationToken);

        return Ok(accounts);
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles(
        CancellationToken cancellationToken)
    {
        var roles = await _context.Roles
            .AsNoTracking()
            .OrderBy(role => role.RoleId)
            .Select(role => new RoleDto
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName
            })
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }

    [HttpPatch("{userId:int}/status")]
    public async Task<IActionResult> UpdateAccountStatus(
        int userId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var statusCode = request.Status?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(statusCode))
        {
            return BadRequest(new
            {
                message = "Trạng thái không được để trống."
            });
        }

        var user = await _context.Users
            .Include(account => account.Status)
            .FirstOrDefaultAsync(
                account => account.UserId == userId,
                cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tài khoản."
            });
        }

        var validStatuses = new[]
        {
            AccountStatusCodes.Active,
            AccountStatusCodes.Suspended,
            AccountStatusCodes.Inactive
        };

        if (!validStatuses.Contains(statusCode))
        {
            return BadRequest(new
            {
                message = "Trạng thái tài khoản không hợp lệ."
            });
        }

        var signedInEmail = User.FindFirst(ClaimTypes.Email)?.Value;

        if (user.Email == signedInEmail && statusCode != AccountStatusCodes.Active)
        {
            return BadRequest(new
            {
                message = "Không thể khóa hoặc ngừng hoạt động tài khoản đang đăng nhập."
            });
        }

        var status = await _statusService.GetStatusAsync(
            StatusGroups.Account, statusCode, cancellationToken);
        if (status is null)
        {
            return BadRequest(new { message = "Trạng thái tài khoản chưa được cấu hình." });
        }

        user.StatusId = status.StatusId;

        await _context.SaveChangesAsync(cancellationToken);

        var message = statusCode switch
        {
            AccountStatusCodes.Active => "Mở khóa tài khoản thành công.",
            AccountStatusCodes.Suspended => "Tạm khóa tài khoản thành công.",
            AccountStatusCodes.Inactive => "Ngừng hoạt động tài khoản thành công.",
            _ => "Cập nhật trạng thái tài khoản thành công."
        };

        return Ok(new
        {
            message,
            userId = user.UserId,
            status = status.StatusCode,
            statusName = status.StatusName
        });
    }

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployeeAccount(
        [FromBody] CreateEmployeeAccountRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();
        var phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : PhoneNumberHelper.Normalize(request.PhoneNumber);

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var phoneNumberExists =
                await _context.Employees.AnyAsync(
                    employee => employee.PhoneNumber == phoneNumber,
                    cancellationToken) ||
                await _context.Customers.AnyAsync(
                    customer => customer.PhoneNumber == phoneNumber,
                    cancellationToken);

            if (phoneNumberExists)
            {
                return Conflict(new
                {
                    message = "Số điện thoại đã được sử dụng."
                });
            }
        }

        var employeeCode = await GenerateEmployeeCodeAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập đầy đủ thông tin bắt buộc."
            });
        }

        var emailExists = await _context.Users.AnyAsync(
            user => user.Email == email,
            cancellationToken);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Email đã được sử dụng."
            });
        }

        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                existingRole => existingRole.RoleId == request.RoleId,
                cancellationToken);

        if (role is null)
        {
            return NotFound(new
            {
                message = "Vai trò không tồn tại."
            });
        }

        if (role.RoleName != RoleNames.Consultant &&
            role.RoleName != RoleNames.Coordinator)
        {
            return BadRequest(new
            {
                message = "Endpoint này chỉ dùng để tạo tài khoản nhân viên tư vấn hoặc nhân viên điều phối."
            });
        }

        if (role.RoleName == RoleNames.Customer)
        {
            return BadRequest(new
            {
                message = "Tài khoản khách hàng phải được tạo qua chức năng đăng ký."
            });
        }

        if (request.Password.Length < 8 ||
            !request.Password.Any(char.IsUpper) ||
            !request.Password.Any(char.IsLower) ||
            !request.Password.Any(char.IsDigit) ||
            !request.Password.Any(character => !char.IsLetterOrDigit(character)))
        {
            return BadRequest(new
            {
                message = "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var accountStatusId = await _statusService.GetStatusIdAsync(
                StatusGroups.Account, AccountStatusCodes.Active, cancellationToken);
            var employeeStatusId = await _statusService.GetStatusIdAsync(
                StatusGroups.Employee, EmployeeStatusCodes.Active, cancellationToken);

            var user = new User
            {
                RoleId = request.RoleId,
                Email = email,
                StatusId = accountStatusId,
                CreatedAt = DateTime.Now
            };

            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            var employee = new Employee
            {
                UserId = user.UserId,
                EmployeeCode = employeeCode,
                FullName = fullName,
                PhoneNumber = phoneNumber,
                StatusId = employeeStatusId
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "Tạo tài khoản nhân viên thành công.",
                userId = user.UserId,
                email = user.Email,
                employeeCode = employee.EmployeeCode,
                fullName = employee.FullName,
                roleName = role.RoleName
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    [HttpPut("employees/{userId:int}")]
    public async Task<IActionResult> UpdateEmployeeAccount(
        int userId,
        [FromBody] UpdateEmployeeAccountRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();
        var phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : PhoneNumberHelper.Normalize(request.PhoneNumber);

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var phoneNumberExists =
                await _context.Employees.AnyAsync(
                    employee =>
                        employee.PhoneNumber == phoneNumber &&
                        employee.UserId != userId,
                    cancellationToken) ||
                await _context.Customers.AnyAsync(
                    customer => customer.PhoneNumber == phoneNumber,
                    cancellationToken);

            if (phoneNumberExists)
            {
                return Conflict(new
                {
                    message = "Số điện thoại đã được sử dụng."
                });
            }
        }

        var employeeStatus = string.IsNullOrWhiteSpace(request.EmployeeStatus)
            ? null
            : request.EmployeeStatus.Trim();

        Status? updatedEmployeeStatus = null;
        if (employeeStatus is not null)
        {
            var validEmployeeStatuses = new[]
            {
                EmployeeStatusCodes.Active,
                EmployeeStatusCodes.OnLeave,
                EmployeeStatusCodes.Terminated
            };

            if (!validEmployeeStatuses.Contains(employeeStatus))
            {
                return BadRequest(new
                {
                    message = "Trạng thái nhân viên không hợp lệ."
                });
            }
        }

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập đầy đủ thông tin bắt buộc."
            });
        }

        if (email.Length > 150)
        {
            return BadRequest(new
            {
                message = "Email không được vượt quá 150 ký tự."
            });
        }

        if (fullName.Length > 150)
        {
            return BadRequest(new
            {
                message = "Họ tên không được vượt quá 150 ký tự."
            });
        }

        if (phoneNumber is not null && phoneNumber.Length > 20)
        {
            return BadRequest(new
            {
                message = "Số điện thoại không được vượt quá 20 ký tự."
            });
        }

        var user = await _context.Users
            .Include(account => account.Status)
            .FirstOrDefaultAsync(
                account => account.UserId == userId,
                cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tài khoản."
            });
        }

        var employee = await _context.Employees
            .Include(existingEmployee => existingEmployee.Status)
            .FirstOrDefaultAsync(
                existingEmployee => existingEmployee.UserId == userId,
                cancellationToken);

        if (employee is null)
        {
            return NotFound(new
            {
                message = "Tài khoản này không phải tài khoản nhân viên."
            });
        }

        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                existingRole => existingRole.RoleId == request.RoleId,
                cancellationToken);

        if (role is null)
        {
            return NotFound(new
            {
                message = "Vai trò không tồn tại."
            });
        }

        if (role.RoleName != RoleNames.Consultant &&
            role.RoleName != RoleNames.Coordinator)
        {
            return BadRequest(new
            {
                message = "Nhân viên chỉ có thể thuộc vai trò Tư vấn hoặc Điều phối."
            });
        }

        var emailExists = await _context.Users.AnyAsync(
            otherUser =>
                otherUser.Email == email &&
                otherUser.UserId != userId,
            cancellationToken);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Email đã được sử dụng."
            });
        }

        user.Email = email;
        user.RoleId = request.RoleId;
        employee.FullName = fullName;
        employee.PhoneNumber = phoneNumber;

        if (employeeStatus is not null)
        {
            var resolvedEmployeeStatus = await _statusService.GetStatusAsync(
                StatusGroups.Employee, employeeStatus, cancellationToken);
            if (resolvedEmployeeStatus is null)
            {
                return BadRequest(new { message = "Trạng thái nhân viên chưa được cấu hình." });
            }

            employee.StatusId = resolvedEmployeeStatus.StatusId;
            updatedEmployeeStatus = resolvedEmployeeStatus;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Cập nhật tài khoản nhân viên thành công.",
            userId = user.UserId,
            email = user.Email,
            employeeCode = employee.EmployeeCode,
            fullName = employee.FullName,
            phoneNumber = employee.PhoneNumber,
            roleName = role.RoleName,
            accountStatus = user.Status.StatusCode,
            accountStatusName = user.Status.StatusName,
            employeeStatus = (updatedEmployeeStatus ?? employee.Status).StatusCode,
            employeeStatusName = (updatedEmployeeStatus ?? employee.Status).StatusName
        });
    }

    [HttpPost("{userId:int}/password-reset")]
    public async Task<IActionResult> SendPasswordResetLink(
        int userId,
        CancellationToken cancellationToken)
    {
        bool userExists;
        try
        {
            userExists = await _passwordResetService.SendResetLinkForUserAsync(
                userId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa thể gửi email đặt lại mật khẩu. Vui lòng kiểm tra cấu hình SMTP."
            });
        }
        if (!userExists)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản." });
        }

        return Ok(new
        {
            message = "Đã gửi liên kết đặt lại mật khẩu đến email của người dùng.",
            userId
        });
    }

    private async Task<string> GenerateEmployeeCodeAsync(
        CancellationToken cancellationToken)
    {
        var employeeCodes = await _context.Employees
            .AsNoTracking()
            .Where(employee => employee.EmployeeCode.StartsWith("NV"))
            .Select(employee => employee.EmployeeCode)
            .ToListAsync(cancellationToken);

        var highestNumber = 0;

        foreach (var employeeCode in employeeCodes)
        {
            if (employeeCode.Length <= 2)
            {
                continue;
            }

            var numericPart = employeeCode[2..];

            if (int.TryParse(numericPart, out var number) &&
                number > highestNumber)
            {
                highestNumber = number;
            }
        }

        return $"NV{highestNumber + 1:D4}";
    }

    [HttpPatch("employees/{userId:int}/status")]
    public async Task<IActionResult> UpdateEmployeeStatus(
        int userId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var statusCode = request.Status?.Trim().ToUpperInvariant();
        var validStatuses = new[]
        {
            EmployeeStatusCodes.Active,
            EmployeeStatusCodes.OnLeave,
            EmployeeStatusCodes.Terminated
        };

        if (string.IsNullOrWhiteSpace(statusCode) ||
            !validStatuses.Contains(statusCode))
        {
            return BadRequest(new
            {
                message = "Trạng thái nhân viên không hợp lệ."
            });
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(
                existingEmployee => existingEmployee.UserId == userId,
                cancellationToken);

        if (employee is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy nhân viên."
            });
        }

        var status = await _statusService.GetStatusAsync(
            StatusGroups.Employee, statusCode, cancellationToken);
        if (status is null)
        {
            return BadRequest(new { message = "Trạng thái nhân viên chưa được cấu hình." });
        }

        employee.StatusId = status.StatusId;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Cập nhật trạng thái nhân viên thành công.",
            userId,
            employeeCode = employee.EmployeeCode,
            employeeStatus = status.StatusCode,
            employeeStatusName = status.StatusName
        });
    }
}
