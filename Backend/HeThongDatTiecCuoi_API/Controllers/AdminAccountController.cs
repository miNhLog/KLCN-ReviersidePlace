using System.Security.Claims;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AdminAccount;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/admin/accounts")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminAccountController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AdminAccountController(
        ApplicationDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
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
            query = query.Where(item => item.user.Status == status);
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
                Status = item.user.Status,
                CreatedAt = item.user.CreatedAt,
                EmployeeStatus = item.employee != null
                    ? item.employee.Status
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
        [FromBody] string status,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
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
            "Hoạt động",
            "Tạm khóa",
            "Ngừng hoạt động"
        };

        if (!validStatuses.Contains(status))
        {
            return BadRequest(new
            {
                message = "Trạng thái tài khoản không hợp lệ."
            });
        }

        var signedInEmail = User.FindFirst(ClaimTypes.Email)?.Value;

        if (user.Email == signedInEmail && status != "Hoạt động")
        {
            return BadRequest(new
            {
                message = "Không thể khóa hoặc ngừng hoạt động tài khoản đang đăng nhập."
            });
        }

        user.Status = status;

        await _context.SaveChangesAsync(cancellationToken);

        var message = status switch
        {
            "Hoạt động" => "Mở khóa tài khoản thành công.",
            "Tạm khóa" => "Tạm khóa tài khoản thành công.",
            "Ngừng hoạt động" => "Ngừng hoạt động tài khoản thành công.",
            _ => "Cập nhật trạng thái tài khoản thành công."
        };

        return Ok(new
        {
            message,
            userId = user.UserId,
            status = user.Status
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
            : request.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var phoneNumberExists = await _context.Employees.AnyAsync(
                employee => employee.PhoneNumber == phoneNumber,
                cancellationToken);

            if (phoneNumberExists)
            {
                return Conflict(new
                {
                    message = "Số điện thoại này đã được sử dụng bởi nhân viên khác."
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
            var user = new User
            {
                RoleId = request.RoleId,
                Email = email,
                Status = "Hoạt động",
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
                Status = "Đang làm việc"
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
            : request.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var phoneNumberExists = await _context.Employees.AnyAsync(
                employee =>
                    employee.PhoneNumber == phoneNumber &&
                    employee.UserId != userId,
                cancellationToken);

            if (phoneNumberExists)
            {
                return Conflict(new
                {
                    message = "Số điện thoại này đã được sử dụng bởi nhân viên khác."
                });
            }
        }

        var employeeStatus = string.IsNullOrWhiteSpace(request.EmployeeStatus)
            ? null
            : request.EmployeeStatus.Trim();

        if (employeeStatus is not null)
        {
            var validEmployeeStatuses = new[]
            {
                "Đang làm việc",
                "Tạm nghỉ",
                "Đã nghỉ việc"
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
            employee.Status = employeeStatus;
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
            accountStatus = user.Status,
            employeeStatus = employee.Status
        });
    }

    [HttpPatch("{userId:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        int userId,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập mật khẩu mới."
            });
        }

        if (request.NewPassword.Length < 8 ||
            !request.NewPassword.Any(char.IsUpper) ||
            !request.NewPassword.Any(char.IsLower) ||
            !request.NewPassword.Any(char.IsDigit) ||
            !request.NewPassword.Any(character => !char.IsLetterOrDigit(character)))
        {
            return BadRequest(new
            {
                message = "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt."
            });
        }

        var user = await _context.Users
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

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.NewPassword);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Đặt lại mật khẩu thành công.",
            userId = user.UserId,
            email = user.Email
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
        [FromBody] string status,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = status?.Trim();
        var validStatuses = new[]
        {
            "Đang làm việc",
            "Tạm nghỉ",
            "Đã nghỉ việc"
        };

        if (string.IsNullOrWhiteSpace(normalizedStatus) ||
            !validStatuses.Contains(normalizedStatus))
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

        employee.Status = normalizedStatus;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Cập nhật trạng thái nhân viên thành công.",
            userId,
            employeeCode = employee.EmployeeCode,
            employeeStatus = employee.Status
        });
    }
}
