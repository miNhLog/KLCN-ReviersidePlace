using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

<<<<<<< Updated upstream
    public DbSet<SanhTiec> SanhTiec => Set<SanhTiec>();
    public DbSet<VaiTro> VaiTro => Set<VaiTro>();
    public DbSet<NguoiDung> NguoiDung => Set<NguoiDung>();
    public DbSet<KhachHang> KhachHang => Set<KhachHang>();
    public DbSet<NhanVien> NhanVien => Set<NhanVien>();

=======
    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<HallSchedule> HallSchedules => Set<HallSchedule>();
    public DbSet<WeddingBooking> WeddingBookings => Set<WeddingBooking>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<MenuDish> MenuDishes => Set<MenuDish>();
    public DbSet<Status> Statuses => Set<Status>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<DecorPackage> DecorPackages { get; set; }
    public DbSet<ServiceItem> ServiceItems { get; set; }
>>>>>>> Stashed changes
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

<<<<<<< Updated upstream
        modelBuilder.Entity<VaiTro>(entity =>
=======
        modelBuilder.Entity<Status>(entity =>
        {
            entity.ToTable("TrangThai");
            entity.HasKey(x => x.StatusId);
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID");
            entity.Property(x => x.StatusCode).HasColumnName("MaTrangThai").HasMaxLength(50).IsRequired();
            entity.Property(x => x.StatusName).HasColumnName("TenTrangThai").HasMaxLength(100).IsRequired();
            entity.Property(x => x.StatusGroup).HasColumnName("NhomTrangThai").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasColumnName("MoTa").HasMaxLength(500);
            entity.HasIndex(x => new { x.StatusGroup, x.StatusCode }).IsUnique();
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("TokenDatLaiMatKhau");
            entity.HasKey(x => x.PasswordResetTokenId);
            entity.Property(x => x.PasswordResetTokenId).HasColumnName("TokenDatLaiMatKhauID");
            entity.Property(x => x.UserId).HasColumnName("NguoiDungID");
            entity.Property(x => x.TokenHash).HasColumnName("TokenHash").HasColumnType("char(64)").IsRequired();
            entity.Property(x => x.ExpiresAt).HasColumnName("HetHanLuc").HasPrecision(0);
            entity.Property(x => x.UsedAt).HasColumnName("DaDungLuc").HasPrecision(0);
            entity.Property(x => x.CreatedAt).HasColumnName("TaoLuc").HasPrecision(0);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.UsedAt });
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExternalLogin>(entity =>
        {
            entity.ToTable("DangNhapNgoai");
            entity.HasKey(x => x.ExternalLoginId);
            entity.Property(x => x.ExternalLoginId).HasColumnName("DangNhapNgoaiID");
            entity.Property(x => x.UserId).HasColumnName("NguoiDungID");
            entity.Property(x => x.Provider)
                .HasColumnName("NhaCungCap")
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(x => x.ProviderUserId)
                .HasColumnName("MaNguoiDungNhaCungCap")
                .HasMaxLength(255)
                .IsRequired();
            entity.Property(x => x.ProviderEmail)
                .HasColumnName("EmailNhaCungCap")
                .HasMaxLength(150);
            entity.Property(x => x.LinkedAt)
                .HasColumnName("NgayLienKet")
                .HasPrecision(0);
            entity.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Provider }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany(x => x.ExternalLogins)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Role>(entity =>
>>>>>>> Stashed changes
        {
            entity.ToTable("VaiTro");
            entity.HasKey(x => x.VaiTroID);
            entity.Property(x => x.TenVaiTro).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.TenVaiTro).IsUnique();
        });

        modelBuilder.Entity<NguoiDung>(entity =>
        {
            entity.ToTable("NguoiDung");
<<<<<<< Updated upstream
            entity.HasKey(x => x.NguoiDungID);
            entity.Property(x => x.Email).HasMaxLength(150).IsRequired();
            entity.Property(x => x.MatKhauHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.TrangThai).HasMaxLength(50).IsRequired();
            entity.Property(x => x.NgayTao).HasPrecision(0);
=======
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("NguoiDungID");
            entity.Property(x => x.RoleId).HasColumnName("VaiTroID");
            entity.Property(x => x.Email).HasColumnName("Email").HasMaxLength(150).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("MatKhauHash").HasMaxLength(255);
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID");
            entity.Property(x => x.CreatedAt).HasColumnName("NgayTao").HasPrecision(0);
>>>>>>> Stashed changes
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasOne(x => x.VaiTro)
                .WithMany(x => x.NguoiDungs)
                .HasForeignKey(x => x.VaiTroID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KhachHang>(entity =>
        {
            entity.ToTable("KhachHang");
<<<<<<< Updated upstream
            entity.HasKey(x => x.KhachHangID);
            entity.Property(x => x.HoTen).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SoDienThoai).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DiaChi).HasMaxLength(255);
            entity.HasIndex(x => x.NguoiDungID).IsUnique().HasFilter("[NguoiDungID] IS NOT NULL");
            entity.HasIndex(x => x.SoDienThoai).IsUnique();
            entity.HasOne(x => x.NguoiDung)
                .WithOne(x => x.KhachHang)
                .HasForeignKey<KhachHang>(x => x.NguoiDungID)
=======
            entity.HasKey(x => x.CustomerId);
            entity.Property(x => x.CustomerId).HasColumnName("KhachHangID");
            entity.Property(x => x.UserId).HasColumnName("NguoiDungID");
            entity.Property(x => x.FullName).HasColumnName("HoTen").HasMaxLength(150).IsRequired();
            entity.Property(x => x.PhoneNumber).HasColumnName("SoDienThoai").HasMaxLength(20);
            entity.HasIndex(x => x.UserId).IsUnique().HasFilter("[NguoiDungID] IS NOT NULL");
            entity.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[SoDienThoai] IS NOT NULL");
            entity.HasOne(x => x.User)
                .WithOne(x => x.Customer)
                .HasForeignKey<Customer>(x => x.UserId)
>>>>>>> Stashed changes
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NhanVien>(entity =>
        {
            entity.ToTable("NhanVien");
            entity.HasKey(x => x.NhanVienID);
            entity.Property(x => x.MaNhanVien).HasMaxLength(50).IsRequired();
            entity.Property(x => x.HoTen).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SoDienThoai).HasMaxLength(20);
            entity.Property(x => x.TrangThai).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.NguoiDungID).IsUnique();
            entity.HasIndex(x => x.MaNhanVien).IsUnique();
            entity.HasIndex(x => x.SoDienThoai).IsUnique().HasFilter("[SoDienThoai] IS NOT NULL");
            entity.HasOne(x => x.NguoiDung)
                .WithOne(x => x.NhanVien)
                .HasForeignKey<NhanVien>(x => x.NguoiDungID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SanhTiec>(entity =>
        {
            entity.ToTable("SanhTiec");
            entity.HasKey(x => x.SanhTiecID);
            entity.Property(x => x.MaSanh).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TenSanh).HasMaxLength(150).IsRequired();
            entity.Property(x => x.GiaThue).HasColumnType("decimal(18,2)");
            entity.Property(x => x.HinhAnh).HasMaxLength(500);
            entity.Property(x => x.TrangThai).HasMaxLength(50).IsRequired();
        });
    }
}
