using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

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
    public DbSet<DecorPackage> DecorPackages { get; set; }
    public DbSet<ServiceItem> ServiceItems { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("VaiTro");
            entity.HasKey(x => x.RoleId);
            entity.Property(x => x.RoleId).HasColumnName("VaiTroID");
            entity.Property(x => x.RoleName)
                .HasColumnName("TenVaiTro")
                .HasMaxLength(100)
                .IsRequired();
            entity.HasIndex(x => x.RoleName).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("NguoiDung");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("NguoiDungID");
            entity.Property(x => x.RoleId).HasColumnName("VaiTroID");
            entity.Property(x => x.Email).HasColumnName("Email").HasMaxLength(150).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("MatKhauHash").HasMaxLength(255).IsRequired();
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID");
            entity.Property(x => x.CreatedAt).HasColumnName("NgayTao").HasPrecision(0);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasOne(x => x.Role)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("KhachHang");
            entity.HasKey(x => x.CustomerId);
            entity.Property(x => x.CustomerId).HasColumnName("KhachHangID");
            entity.Property(x => x.UserId).HasColumnName("NguoiDungID");
            entity.Property(x => x.FullName).HasColumnName("HoTen").HasMaxLength(150).IsRequired();
            entity.Property(x => x.PhoneNumber).HasColumnName("SoDienThoai").HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.UserId).IsUnique().HasFilter("[NguoiDungID] IS NOT NULL");
            entity.HasIndex(x => x.PhoneNumber).IsUnique();
            entity.HasOne(x => x.User)
                .WithOne(x => x.Customer)
                .HasForeignKey<Customer>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("NhanVien");
            entity.HasKey(x => x.EmployeeId);
            entity.Property(x => x.EmployeeId).HasColumnName("NhanVienID");
            entity.Property(x => x.UserId).HasColumnName("NguoiDungID");
            entity.Property(x => x.EmployeeCode).HasColumnName("MaNhanVien").HasMaxLength(50).IsRequired();
            entity.Property(x => x.FullName).HasColumnName("HoTen").HasMaxLength(150).IsRequired();
            entity.Property(x => x.PhoneNumber).HasColumnName("SoDienThoai").HasMaxLength(20);
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID");
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.EmployeeCode).IsUnique();
            entity.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[SoDienThoai] IS NOT NULL");
            entity.HasOne(x => x.User)
                .WithOne(x => x.Employee)
                .HasForeignKey<Employee>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Hall>(entity =>
        {
            entity.ToTable("SanhTiec");
            entity.HasKey(x => x.HallId);
            entity.Property(x => x.HallId).HasColumnName("SanhTiecID");
            entity.Property(x => x.HallCode).HasColumnName("MaSanh").HasMaxLength(50).IsRequired();
            entity.Property(x => x.HallName).HasColumnName("TenSanh").HasMaxLength(150).IsRequired();
            entity.Property(x => x.MinimumCapacity).HasColumnName("SucChuaToiThieu");
            entity.Property(x => x.MaximumCapacity).HasColumnName("SucChuaToiDa");
            entity.Property(x => x.RentalPrice).HasColumnName("GiaThue").HasColumnType("decimal(18,2)");
            entity.Property(x => x.Description).HasColumnName("MoTa");
            entity.Property(x => x.ImageUrl).HasColumnName("HinhAnh").HasMaxLength(500);
            entity.Property(x => x.StatusId).HasColumnName("TrangThaiID");
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HallSchedule>(entity =>
        {
            entity.ToTable("LichSanh");

            entity.HasKey(x => x.HallScheduleId);

            entity.Property(x => x.HallScheduleId)
                .HasColumnName("LichSanhID");

            entity.Property(x => x.HallId)
                .HasColumnName("SanhTiecID");

            entity.Property(x => x.Date)
                .HasColumnName("Ngay")
                .HasColumnType("date");

            entity.Property(x => x.Shift)
                .HasColumnName("CaToChuc")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.StatusId)
                .HasColumnName("TrangThaiID");

            entity.Property(x => x.Notes)
                .HasColumnName("GhiChu")
                .HasMaxLength(500);

            entity.HasIndex(x => new
            {
                x.HallId,
                x.Date,
                x.Shift
            }).IsUnique();

            entity.HasOne(x => x.Hall)
                .WithMany()
                .HasForeignKey(x => x.HallId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WeddingBooking>(entity =>
        {
            entity.ToTable("DatTiec");

            entity.HasKey(x => x.BookingId);
            entity.Property(x => x.BookingId).HasColumnName("DatTiecID");
            entity.Property(x => x.BookingCode)
                .HasColumnName("MaDatTiec")
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(x => x.BookingCode)
                .IsUnique();
            entity.Property(x => x.CustomerId).HasColumnName("KhachHangID");
            entity.Property(x => x.HallScheduleId).HasColumnName("LichSanhID");
            entity.Property(x => x.MenuId).HasColumnName("ThucDonID");
            entity.Property(x => x.DecorationPackageId).HasColumnName("GoiTrangTriID");
            entity.Property(x => x.ConsultantEmployeeId).HasColumnName("NhanVienTuVanID");
            entity.Property(x => x.ExpectedBudget).HasColumnName("NganSachDuKien")
                .HasColumnType("decimal(18,2)");
            entity.Property(x => x.DesiredStyle).HasColumnName("PhongCachMongMuon")
                .HasMaxLength(100);
            entity.Property(x => x.GuestCount).HasColumnName("SoLuongKhach");
            entity.Property(x => x.TableCount).HasColumnName("SoBan");
            entity.Property(x => x.FinalMenuPrice).HasColumnName("GiaThucDonChot")
                .HasColumnType("decimal(18,2)");
            entity.Property(x => x.FinalDecorationPrice).HasColumnName("GiaTrangTriChot")
                .HasColumnType("decimal(18,2)");
            entity.Property(x => x.FinalHallPrice).HasColumnName("GiaSanhChot")
                .HasColumnType("decimal(18,2)");
            entity.Property(x => x.EstimatedTotal).HasColumnName("TongTienDuKien")
                .HasColumnType("decimal(18,2)");
            entity.Property(x => x.SpecialRequests).HasColumnName("YeuCauDacBiet");
            entity.Property(x => x.Status).HasColumnName("TrangThai")
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(x => x.CancellationReason).HasColumnName("LyDoHuy")
                .HasMaxLength(500);
            entity.Property(x => x.BookedAt).HasColumnName("NgayDat")
                .HasPrecision(0);
            entity.Property(x => x.UpdatedAt).HasColumnName("NgayCapNhat")
                .HasPrecision(0);
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HallSchedule)
                .WithMany()
                .HasForeignKey(x => x.HallScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Menu>(entity =>
        {
            entity.ToTable("ThucDon");

            entity.HasKey(x => x.MenuId);

            entity.Property(x => x.MenuId)
                .HasColumnName("ThucDonID");

            entity.Property(x => x.MenuCode)
                .HasColumnName("MaThucDon")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.MenuName)
                .HasColumnName("TenThucDon")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasColumnName("MoTa");

            entity.Property(x => x.PricePerTable)
                .HasColumnName("GiaMoiBan")
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(x => x.StatusId)
                .HasColumnName("TrangThaiID");

            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.MenuCode)
                .IsUnique();
        });


        modelBuilder.Entity<Dish>(entity =>
        {
            entity.ToTable("MonAn");

            entity.HasKey(x => x.DishId);

            entity.Property(x => x.DishId)
                .HasColumnName("MonAnID");

            entity.Property(x => x.DishCode)
                .HasColumnName("MaMon")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.DishName)
                .HasColumnName("TenMon")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Category)
                .HasColumnName("NhomMon")
                .HasMaxLength(100);

            entity.Property(x => x.ImageUrl)
                .HasColumnName("HinhAnh")
                .HasMaxLength(500);

            entity.Property(x => x.StatusId)
                .HasColumnName("TrangThaiID");

            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.DishCode)
                .IsUnique();
        });


        modelBuilder.Entity<MenuDish>(entity =>
        {
            entity.ToTable("ChiTietThucDon");

            entity.HasKey(x => x.MenuDishId);

            entity.Property(x => x.MenuDishId)
                .HasColumnName("ChiTietThucDonID");

            entity.Property(x => x.MenuId)
                .HasColumnName("ThucDonID");

            entity.Property(x => x.DishId)
                .HasColumnName("MonAnID");

            entity.Property(x => x.SortOrder)
                .HasColumnName("SoThuTu")
                .IsRequired()
                .HasDefaultValue(1);

            entity.HasIndex(x => new
            {
                x.MenuId,
                x.DishId
            })
            .IsUnique();

            entity.HasIndex(x => new
            {
                x.MenuId,
                x.SortOrder
            })
            .IsUnique();

            entity.HasOne(x => x.Menu)
                .WithMany(x => x.MenuDishes)
                .HasForeignKey(x => x.MenuId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Dish)
                .WithMany(x => x.MenuDishes)
                .HasForeignKey(x => x.DishId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

}
