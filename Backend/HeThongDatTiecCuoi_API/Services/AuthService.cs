using System.Text.RegularExpressions;
<<<<<<< Updated upstream
=======
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
>>>>>>> Stashed changes
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed partial class AuthService : IAuthService
{
    private const string GoogleProvider = "Google";

    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<NguoiDung> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
<<<<<<< Updated upstream

    public AuthService(
        ApplicationDbContext db,
        IPasswordHasher<NguoiDung> passwordHasher,
        IJwtTokenService jwtTokenService)
=======
    private readonly IStatusService _statusService;
    private readonly IGoogleTokenValidator _googleTokenValidator;

    public AuthService(
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService jwtTokenService,
        IStatusService statusService,
        IGoogleTokenValidator googleTokenValidator)
>>>>>>> Stashed changes
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
<<<<<<< Updated upstream
=======
        _statusService = statusService;
        _googleTokenValidator = googleTokenValidator;
>>>>>>> Stashed changes
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

        if (await _db.NguoiDung.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Email đã được sử dụng.",
                StatusCodes.Status409Conflict);
        }

        if (await _db.KhachHang.AnyAsync(x => x.SoDienThoai == phone, cancellationToken))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Số điện thoại đã được sử dụng.",
                StatusCodes.Status409Conflict);
        }

        var customerRole = await _db.VaiTro.SingleOrDefaultAsync(
            x => x.TenVaiTro == RoleNames.Customer,
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
            var user = new NguoiDung
            {
                VaiTroID = customerRole.VaiTroID,
                VaiTro = customerRole,
                Email = email,
<<<<<<< Updated upstream
                TrangThai = "Hoạt động",
                NgayTao = DateTime.Now
=======
                StatusId = accountStatusId,
                CreatedAt = DateTime.UtcNow
>>>>>>> Stashed changes
            };
            user.MatKhauHash = _passwordHasher.HashPassword(user, request.MatKhau);

            var customer = new KhachHang
            {
                NguoiDung = user,
                HoTen = request.HoTen.Trim(),
                SoDienThoai = phone
            };

            _db.NguoiDung.Add(user);
            _db.KhachHang.Add(customer);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

<<<<<<< Updated upstream
            user.KhachHang = customer;
            var token = _jwtTokenService.CreateAccessToken(user, rememberMe: false);
            return ServiceResult<AuthResponse>.Success(
                new AuthResponse(token.Token, token.ExpiresAtUtc, ToCurrentUser(user)),
                StatusCodes.Status201Created);
=======
            var hydratedUser = await UserQuery()
                .SingleAsync(x => x.UserId == user.UserId, cancellationToken);
            return CreateAuthenticationResult(hydratedUser, rememberMe: false, StatusCodes.Status201Created);
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
        var identifier = request.DinhDanh.Trim();
        var isStaffLogin = request.LoaiTaiKhoan == "Staff";

        IQueryable<NguoiDung> query = _db.NguoiDung
            .Include(x => x.VaiTro)
            .Include(x => x.KhachHang)
            .Include(x => x.NhanVien);

        NguoiDung? user;
=======
        var identifier = request.Identifier.Trim();
        User? user;

>>>>>>> Stashed changes
        if (identifier.Contains('@'))
        {
            var email = identifier.ToLowerInvariant();
            user = await UserQuery()
                .SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        }
        else
        {
<<<<<<< Updated upstream
            var phone = NormalizePhone(identifier);
            user = isStaffLogin
                ? await query.SingleOrDefaultAsync(x => x.NhanVien != null && x.NhanVien.SoDienThoai == phone, cancellationToken)
                : await query.SingleOrDefaultAsync(x => x.KhachHang != null && x.KhachHang.SoDienThoai == phone, cancellationToken);
=======
            var phone = PhoneNumberHelper.Normalize(identifier);
            var matches = await UserQuery()
                .Where(x =>
                    (x.Customer != null && x.Customer.PhoneNumber == phone) ||
                    (x.Employee != null && x.Employee.PhoneNumber == phone))
                .Take(2)
                .ToListAsync(cancellationToken);

            if (matches.Count > 1)
            {
                return ServiceResult<AuthResponse>.Failure(
                    "Số điện thoại đang gắn với nhiều tài khoản. Vui lòng đăng nhập bằng email.",
                    StatusCodes.Status409Conflict);
            }

            user = matches.SingleOrDefault();
>>>>>>> Stashed changes
        }

        if (user is null)
        {
            return InvalidCredentials();
        }

<<<<<<< Updated upstream
        if (user.TrangThai != "Hoạt động")
=======
        var accountFailure = ValidateAccountCanSignIn(user);
        if (accountFailure is not null)
>>>>>>> Stashed changes
        {
            return accountFailure;
        }

<<<<<<< Updated upstream
        if (isStaffLogin && user.NhanVien is not null && user.NhanVien.TrangThai != "Đang làm việc")
=======
        var passwordHash = user.PasswordHash;
        if (string.IsNullOrWhiteSpace(passwordHash))
>>>>>>> Stashed changes
        {
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản này chưa có mật khẩu. Hãy đăng nhập bằng Google hoặc dùng Quên mật khẩu để tạo mật khẩu.",
                StatusCodes.Status401Unauthorized);
        }

        PasswordVerificationResult verification;
        try
        {
<<<<<<< Updated upstream
            verification = _passwordHasher.VerifyHashedPassword(user, user.MatKhauHash, request.MatKhau);
=======
            verification = _passwordHasher.VerifyHashedPassword(
                user,
                passwordHash,
                request.Password);
>>>>>>> Stashed changes
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
            user.MatKhauHash = _passwordHasher.HashPassword(user, request.MatKhau);
            await _db.SaveChangesAsync(cancellationToken);
        }

<<<<<<< Updated upstream
        var token = _jwtTokenService.CreateAccessToken(user, request.GhiNhoDangNhap);
        return ServiceResult<AuthResponse>.Success(
            new AuthResponse(token.Token, token.ExpiresAtUtc, ToCurrentUser(user)));
=======
        return CreateAuthenticationResult(user, request.RememberMe);
    }

    public async Task<ServiceResult<AuthResponse>> GoogleLoginAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        GoogleUserInfo? googleUser;
        try
        {
            googleUser = await _googleTokenValidator.ValidateAsync(
                request.Credential,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return ServiceResult<AuthResponse>.Failure(
                exception.Message,
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Không thể xác minh tài khoản Google lúc này. Vui lòng thử lại.",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (googleUser is null)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Thông tin xác thực Google không hợp lệ hoặc đã hết hạn.",
                StatusCodes.Status401Unauthorized);
        }

        var linkedLogin = await _db.ExternalLogins
            .Include(x => x.User)
                .ThenInclude(x => x.Role)
            .Include(x => x.User)
                .ThenInclude(x => x.Status)
            .Include(x => x.User)
                .ThenInclude(x => x.Customer)
            .Include(x => x.User)
                .ThenInclude(x => x.Employee)
                    .ThenInclude(x => x!.Status)
            .SingleOrDefaultAsync(
                x => x.Provider == GoogleProvider &&
                     x.ProviderUserId == googleUser.Subject,
                cancellationToken);

        if (linkedLogin is not null)
        {
            var accountFailure = ValidateAccountCanSignIn(linkedLogin.User);
            if (accountFailure is not null)
            {
                return accountFailure;
            }

            if (!string.Equals(
                    linkedLogin.ProviderEmail,
                    googleUser.Email,
                    StringComparison.OrdinalIgnoreCase))
            {
                linkedLogin.ProviderEmail = googleUser.Email;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return CreateAuthenticationResult(linkedLogin.User, request.RememberMe);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await UserQuery()
                .SingleOrDefaultAsync(x => x.Email == googleUser.Email, cancellationToken);

            if (user is not null)
            {
                var accountFailure = ValidateAccountCanSignIn(user);
                if (accountFailure is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return accountFailure;
                }

                var alreadyLinked = await _db.ExternalLogins.AnyAsync(
                    x => x.UserId == user.UserId && x.Provider == GoogleProvider,
                    cancellationToken);
                if (alreadyLinked)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<AuthResponse>.Failure(
                        "Email này đã liên kết với một tài khoản Google khác.",
                        StatusCodes.Status409Conflict);
                }

                if (user.Role.RoleName == RoleNames.Customer && user.Customer is null)
                {
                    user.Customer = new Customer
                    {
                        User = user,
                        FullName = googleUser.DisplayName,
                        PhoneNumber = null
                    };
                }
            }
            else
            {
                var customerRole = await _db.Roles.SingleOrDefaultAsync(
                    x => x.RoleName == RoleNames.Customer,
                    cancellationToken);
                if (customerRole is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<AuthResponse>.Failure(
                        "Hệ thống chưa có vai trò Khách hàng.",
                        StatusCodes.Status500InternalServerError);
                }

                var accountStatusId = await _statusService.GetStatusIdAsync(
                    StatusGroups.Account,
                    AccountStatusCodes.Active,
                    cancellationToken);

                user = new User
                {
                    RoleId = customerRole.RoleId,
                    Role = customerRole,
                    Email = googleUser.Email,
                    PasswordHash = null,
                    StatusId = accountStatusId,
                    CreatedAt = DateTime.UtcNow,
                    Customer = new Customer
                    {
                        FullName = googleUser.DisplayName,
                        PhoneNumber = null
                    }
                };
                _db.Users.Add(user);
            }

            _db.ExternalLogins.Add(new ExternalLogin
            {
                User = user,
                Provider = GoogleProvider,
                ProviderUserId = googleUser.Subject,
                ProviderEmail = googleUser.Email,
                LinkedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var hydratedUser = await UserQuery()
                .SingleAsync(x => x.UserId == user.UserId, cancellationToken);
            return CreateAuthenticationResult(hydratedUser, request.RememberMe);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<AuthResponse>.Failure(
                "Không thể liên kết tài khoản Google. Tài khoản có thể vừa được sử dụng ở một phiên khác.",
                StatusCodes.Status409Conflict);
        }
>>>>>>> Stashed changes
    }

    public async Task<ServiceResult<CurrentUserResponse>> GetCurrentUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
<<<<<<< Updated upstream
        var user = await _db.NguoiDung
            .AsNoTracking()
            .Include(x => x.VaiTro)
            .Include(x => x.KhachHang)
            .Include(x => x.NhanVien)
            .SingleOrDefaultAsync(x => x.NguoiDungID == userId, cancellationToken);
=======
        var user = await UserQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
>>>>>>> Stashed changes

        return user is null
            ? ServiceResult<CurrentUserResponse>.Failure(
                "Không tìm thấy người dùng.",
                StatusCodes.Status404NotFound)
            : ServiceResult<CurrentUserResponse>.Success(ToCurrentUser(user));
    }

<<<<<<< Updated upstream
    private static bool MatchesSelectedAccountType(
    NguoiDung user,
    bool isStaffLogin) =>
    isStaffLogin
        ? user.VaiTro.TenVaiTro is
            RoleNames.Admin or
            RoleNames.Staff or
            RoleNames.Coordinator
        : user.VaiTro.TenVaiTro == RoleNames.Customer;
=======
    private IQueryable<User> UserQuery() => _db.Users
        .Include(x => x.Role)
        .Include(x => x.Status)
        .Include(x => x.Customer)
        .Include(x => x.Employee)
            .ThenInclude(x => x!.Status);

    private static ServiceResult<AuthResponse>? ValidateAccountCanSignIn(User user)
    {
        if (user.Status.StatusCode != AccountStatusCodes.Active)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản đang bị khóa hoặc đã ngừng hoạt động.",
                StatusCodes.Status403Forbidden);
        }

        if (user.Role.RoleName is RoleNames.Consultant or RoleNames.Coordinator)
        {
            if (user.Employee is null ||
                user.Employee.Status.StatusCode != EmployeeStatusCodes.Active)
            {
                return ServiceResult<AuthResponse>.Failure(
                    "Tài khoản nhân viên hiện không được phép đăng nhập.",
                    StatusCodes.Status403Forbidden);
            }
        }

        return null;
    }

    private ServiceResult<AuthResponse> CreateAuthenticationResult(
        User user,
        bool rememberMe,
        int statusCode = StatusCodes.Status200OK)
    {
        var token = _jwtTokenService.CreateAccessToken(user, rememberMe);
        return ServiceResult<AuthResponse>.Success(
            new AuthResponse(token.Token, token.ExpiresAtUtc, ToCurrentUser(user)),
            statusCode);
    }
>>>>>>> Stashed changes

    private static CurrentUserResponse ToCurrentUser(NguoiDung user) => new(
        user.NguoiDungID,
        user.Email,
        user.KhachHang?.HoTen ?? user.NhanVien?.HoTen ?? user.Email,
        user.KhachHang?.SoDienThoai ?? user.NhanVien?.SoDienThoai,
        user.VaiTro.TenVaiTro,
        user.TrangThai);

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
