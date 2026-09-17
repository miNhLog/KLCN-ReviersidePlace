using System.Text.RegularExpressions;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.Helpers;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed partial class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IStatusService _statusService;

    public AuthService(
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService jwtTokenService,
        IStatusService statusService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _statusService = statusService;
    }

    public async Task<ServiceResult<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = PhoneNumberHelper.Normalize(request.PhoneNumber);

        if (!StrongPasswordRegex().IsMatch(request.Password))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Mật khẩu phải có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.",
                StatusCodes.Status400BadRequest);
        }

        if (await _db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Email đã được sử dụng.",
                StatusCodes.Status409Conflict);
        }

        var phoneExists =
            await _db.Customers.AnyAsync(
                customer => customer.PhoneNumber == phone,
                cancellationToken) ||
            await _db.Employees.AnyAsync(
                employee => employee.PhoneNumber == phone,
                cancellationToken);

        if (phoneExists)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Số điện thoại đã được sử dụng.",
                StatusCodes.Status409Conflict);
        }

        var customerRole = await _db.Roles.SingleOrDefaultAsync(
            x => x.RoleName == RoleNames.Customer,
            cancellationToken);

        if (customerRole is null)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Hệ thống chưa có vai trò Khách hàng. Vui lòng chạy script khởi tạo dữ liệu.",
                StatusCodes.Status500InternalServerError);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var accountStatusId = await _statusService.GetStatusIdAsync(
                StatusGroups.Account,
                AccountStatusCodes.Active,
                cancellationToken);

            var user = new User
            {
                RoleId = customerRole.RoleId,
                Role = customerRole,
                Email = email,
                StatusId = accountStatusId,
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            var customer = new Customer
            {
                User = user,
                FullName = request.FullName.Trim(),
                PhoneNumber = phone
            };

            _db.Users.Add(user);
            _db.Customers.Add(customer);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Load Status navigation sau khi User đã được lưu
            await _db.Entry(user)
                .Reference(x => x.Status)
                .LoadAsync(cancellationToken);

            user.Customer = customer;

            var token = _jwtTokenService.CreateAccessToken(
                user,
                rememberMe: false);

            return ServiceResult<AuthResponse>.Success(
                new AuthResponse(
                    token.Token,
                    token.ExpiresAtUtc,
                    ToCurrentUser(user)),
                StatusCodes.Status201Created);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<AuthResponse>.Failure(
                "Email hoặc số điện thoại đã được sử dụng.",
                StatusCodes.Status409Conflict);
        }
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var isStaffLogin = request.AccountType == "Staff";

        IQueryable<User> query = _db.Users
            .Include(x => x.Role)
            .Include(x => x.Status)
            .Include(x => x.Customer)
            .Include(x => x.Employee)
                .ThenInclude(x => x!.Status);

        User? user;
        if (identifier.Contains('@'))
        {
            var email = identifier.ToLowerInvariant();
            user = await query.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        }
        else
        {
            var phone = PhoneNumberHelper.Normalize(identifier);
            user = isStaffLogin
                ? await query.SingleOrDefaultAsync(x => x.Employee != null && x.Employee.PhoneNumber == phone, cancellationToken)
                : await query.SingleOrDefaultAsync(x => x.Customer != null && x.Customer.PhoneNumber == phone, cancellationToken);
        }

        if (user is null || !MatchesSelectedAccountType(user, isStaffLogin))
        {
            return InvalidCredentials();
        }

        if (user.Status.StatusCode != AccountStatusCodes.Active)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản đang bị khóa hoặc đã ngừng hoạt động.",
                StatusCodes.Status403Forbidden);
        }

        if (isStaffLogin &&
            user.Employee is not null &&
            user.Employee.Status.StatusCode != EmployeeStatusCodes.Active)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản nhân viên hiện không được phép đăng nhập.",
                StatusCodes.Status403Forbidden);
        }


        PasswordVerificationResult verification;
        try
        {
            verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        }
        catch (FormatException)
        {
            return InvalidCredentials();
        }
        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var token = _jwtTokenService.CreateAccessToken(user, request.RememberMe);
        return ServiceResult<AuthResponse>.Success(
            new AuthResponse(token.Token, token.ExpiresAtUtc, ToCurrentUser(user)));
    }

    public async Task<ServiceResult<CurrentUserResponse>> GetCurrentUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.Status)
            .Include(x => x.Customer)
            .Include(x => x.Employee)
                .ThenInclude(x => x!.Status)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        return user is null
            ? ServiceResult<CurrentUserResponse>.Failure("Không tìm thấy người dùng.", StatusCodes.Status404NotFound)
            : ServiceResult<CurrentUserResponse>.Success(ToCurrentUser(user));
    }

    private static bool MatchesSelectedAccountType(
    User user,
    bool isStaffLogin) =>
    isStaffLogin
        ? user.Role.RoleName is
            RoleNames.Admin or
            RoleNames.Consultant or
            RoleNames.Coordinator
        : user.Role.RoleName == RoleNames.Customer;

    private static CurrentUserResponse ToCurrentUser(User user) => new(
        user.UserId,
        user.Email,
        user.Customer?.FullName ?? user.Employee?.FullName ?? user.Email,
        user.Customer?.PhoneNumber ?? user.Employee?.PhoneNumber,
        user.Role.RoleName,
        user.Status.StatusCode);

    private static ServiceResult<AuthResponse> InvalidCredentials() =>
        ServiceResult<AuthResponse>.Failure(
            "Thông tin đăng nhập không chính xác.",
            StatusCodes.Status401Unauthorized);

    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,100}$")]
    private static partial Regex StrongPasswordRegex();
}
