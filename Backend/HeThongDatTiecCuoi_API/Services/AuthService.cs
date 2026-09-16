using System.Text.RegularExpressions;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed partial class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<ServiceResult<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = NormalizePhone(request.SoDienThoai);

        if (!StrongPasswordRegex().IsMatch(request.MatKhau))
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

        if (await _db.Customers.AnyAsync(x => x.PhoneNumber == phone, cancellationToken))
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
            var user = new User
            {
                RoleId = customerRole.RoleId,
                Role = customerRole,
                Email = email,
                Status = "Hoạt động",
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, request.MatKhau);

            var customer = new Customer
            {
                User = user,
                FullName = request.HoTen.Trim(),
                PhoneNumber = phone
            };

            _db.Users.Add(user);
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            user.Customer = customer;
            var token = _jwtTokenService.CreateAccessToken(user, rememberMe: false);
            return ServiceResult<AuthResponse>.Success(
                new AuthResponse(token.Token, token.ExpiresAtUtc, ToCurrentUser(user)),
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
        var identifier = request.DinhDanh.Trim();
        var isStaffLogin = request.LoaiTaiKhoan == "Staff";

        IQueryable<User> query = _db.Users
            .Include(x => x.Role)
            .Include(x => x.Customer)
            .Include(x => x.Employee);

        User? user;
        if (identifier.Contains('@'))
        {
            var email = identifier.ToLowerInvariant();
            user = await query.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        }
        else
        {
            var phone = NormalizePhone(identifier);
            user = isStaffLogin
                ? await query.SingleOrDefaultAsync(x => x.Employee != null && x.Employee.PhoneNumber == phone, cancellationToken)
                : await query.SingleOrDefaultAsync(x => x.Customer != null && x.Customer.PhoneNumber == phone, cancellationToken);
        }

        if (user is null || !MatchesSelectedAccountType(user, isStaffLogin))
        {
            return InvalidCredentials();
        }

        if (user.Status != "Hoạt động")
        {
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản đang bị khóa hoặc đã ngừng hoạt động.",
                StatusCodes.Status403Forbidden);
        }

        if (isStaffLogin && user.Employee is not null && user.Employee.Status != "Đang làm việc")
        {
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản nhân viên hiện không được phép đăng nhập.",
                StatusCodes.Status403Forbidden);
        }


        PasswordVerificationResult verification;
        try
        {
            verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.MatKhau);
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
            user.PasswordHash = _passwordHasher.HashPassword(user, request.MatKhau);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var token = _jwtTokenService.CreateAccessToken(user, request.GhiNhoDangNhap);
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
            .Include(x => x.Customer)
            .Include(x => x.Employee)
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
        user.Status);

    private static ServiceResult<AuthResponse> InvalidCredentials() =>
        ServiceResult<AuthResponse>.Failure(
            "Thông tin đăng nhập không chính xác.",
            StatusCodes.Status401Unauthorized);

    private static string NormalizePhone(string value)
    {
        var digits = NonDigitRegex().Replace(value, string.Empty);
        return digits.StartsWith("84") && digits.Length == 11
            ? $"0{digits[2..]}"
            : digits;
    }

    [GeneratedRegex(@"[^0-9]")]
    private static partial Regex NonDigitRegex();

    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,100}$")]
    private static partial Regex StrongPasswordRegex();
}
