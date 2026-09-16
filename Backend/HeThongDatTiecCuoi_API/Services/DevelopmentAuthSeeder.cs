using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class DevelopmentAuthSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly DevelopmentAccountsOptions _options;

    public DevelopmentAuthSeeder(
        ApplicationDbContext db,
        IPasswordHasher<User> hasher,
        IOptions<DevelopmentAccountsOptions> options)
    {
        _db = db;
        _hasher = hasher;
        _options = options.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var roles = await EnsureRolesAsync(cancellationToken);
        await UpsertAccountAsync(_options.AdminEmail, _options.AdminPassword, roles[RoleNames.Admin], cancellationToken);
        await UpsertAccountAsync(_options.StaffEmail, _options.StaffPassword, roles[RoleNames.Consultant], cancellationToken);
        await UpsertAccountAsync(_options.CustomerEmail, _options.CustomerPassword, roles[RoleNames.Customer], cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, Role>> EnsureRolesAsync(CancellationToken cancellationToken)
    {
        var required = new[] { RoleNames.Admin, RoleNames.Consultant, RoleNames.Customer };
        var roles = await _db.Roles
            .Where(role => required.Contains(role.RoleName))
            .ToDictionaryAsync(role => role.RoleName, cancellationToken);

        foreach (var name in required.Where(name => !roles.ContainsKey(name)))
        {
            var role = new Role { RoleName = name };
            _db.Roles.Add(role);
            roles[name] = role;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return roles;
    }

    private async Task UpsertAccountAsync(
        string email,
        string password,
        Role role,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(x => x.Customer)
            .Include(x => x.Employee)
            .SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Email = normalizedEmail,
                Role = role,
                RoleId = role.RoleId,
                Status = "Hoạt động",
                CreatedAt = DateTime.Now
            };
            _db.Users.Add(user);
        }
        else
        {
            user.Role = role;
            user.RoleId = role.RoleId;
            user.Status = "Hoạt động";
        }

        user.PasswordHash = _hasher.HashPassword(user, password);

        if (role.RoleName == RoleNames.Consultant && user.Employee is null)
        {
            user.Employee = new Employee
            {
                User = user,
                EmployeeCode = "NV001",
                FullName = "Nhân viên tư vấn",
                PhoneNumber = "0912345678",
                Status = "Đang làm việc"
            };
        }

        if (role.RoleName == RoleNames.Customer && user.Customer is null)
        {
            user.Customer = new Customer
            {
                User = user,
                FullName = "Khách hàng mẫu",
                PhoneNumber = "0901234567"
            };
        }
    }
}
