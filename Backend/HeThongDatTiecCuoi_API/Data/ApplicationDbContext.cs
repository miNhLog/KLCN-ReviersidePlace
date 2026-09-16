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
    public DbSet<VaiTro> VaiTro => Set<VaiTro>();
    public DbSet<NguoiDung> NguoiDung => Set<NguoiDung>();
    public DbSet<KhachHang> KhachHang => Set<KhachHang>();
    public DbSet<NhanVien> NhanVien => Set<NhanVien>();
    public DbSet<HallSchedule> HallSchedules => Set<HallSchedule>();
    public DbSet<DatTiec> DatTiec => Set<DatTiec>();
    public DbSet<ThucDon> ThucDon => Set<ThucDon>();

    public DbSet<MonAn> MonAn => Set<MonAn>();

    public DbSet<ChiTietThucDon> ChiTietThucDon => Set<ChiTietThucDon>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VaiTro>(entity =>
        {
            entity.ToTable("VaiTro");
            entity.HasKey(x => x.VaiTroID);
            entity.Property(x => x.TenVaiTro).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.TenVaiTro).IsUnique();
        });

        modelBuilder.Entity<NguoiDung>(entity =>
        {
            entity.ToTable("NguoiDung");
            entity.HasKey(x => x.NguoiDungID);
            entity.Property(x => x.Email).HasMaxLength(150).IsRequired();
            entity.Property(x => x.MatKhauHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.TrangThai).HasMaxLength(50).IsRequired();
            entity.Property(x => x.NgayTao).HasPrecision(0);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasOne(x => x.VaiTro)
                .WithMany(x => x.NguoiDungs)
                .HasForeignKey(x => x.VaiTroID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KhachHang>(entity =>
        {
            entity.ToTable("KhachHang");
            entity.HasKey(x => x.KhachHangID);
            entity.Property(x => x.HoTen).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SoDienThoai).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.NguoiDungID).IsUnique().HasFilter("[NguoiDungID] IS NOT NULL");
            entity.HasIndex(x => x.SoDienThoai).IsUnique();
            entity.HasOne(x => x.NguoiDung)
                .WithOne(x => x.KhachHang)
                .HasForeignKey<KhachHang>(x => x.NguoiDungID)
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
            entity.Property(x => x.Status).HasColumnName("TrangThai").HasMaxLength(50).IsRequired();
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

            entity.Property(x => x.Status)
                .HasColumnName("TrangThai")
                .HasMaxLength(50)
                .IsRequired();

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
        });
        modelBuilder.Entity<DatTiec>(entity =>
        {
            entity.ToTable("DatTiec");

            entity.HasKey(x => x.DatTiecID);

            entity.Property(x => x.MaDatTiec)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(x => x.MaDatTiec)
                .IsUnique();

            entity.Property(x => x.NganSachDuKien)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.PhongCachMongMuon)
                .HasMaxLength(100);

            entity.Property(x => x.GiaThucDonChot)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.GiaTrangTriChot)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.GiaSanhChot)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TongTienDuKien)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TrangThai)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.LyDoHuy)
                .HasMaxLength(500);

            entity.Property(x => x.NgayDat)
                .HasPrecision(0);

            entity.Property(x => x.NgayCapNhat)
                .HasPrecision(0);

            entity.HasOne(x => x.KhachHang)
                .WithMany()
                .HasForeignKey(x => x.KhachHangID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.HallScheduleId)
                .HasColumnName("LichSanhID");

            entity.HasOne(x => x.HallSchedule)
                .WithMany()
                .HasForeignKey(x => x.HallScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ThucDon>(entity =>
        {
            entity.ToTable("ThucDon");

            entity.HasKey(x => x.ThucDonID);

            entity.Property(x => x.MaThucDon)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.TenThucDon)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.MoTa);

            entity.Property(x => x.GiaMoiBan)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(x => x.TrangThai)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValue("Áp dụng");

            entity.HasIndex(x => x.MaThucDon)
                .IsUnique();
        });


        modelBuilder.Entity<MonAn>(entity =>
        {
            entity.ToTable("MonAn");

            entity.HasKey(x => x.MonAnID);

            entity.Property(x => x.MaMon)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.TenMon)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.NhomMon)
                .HasMaxLength(100);

            entity.Property(x => x.HinhAnh)
                .HasMaxLength(500);

            entity.Property(x => x.TrangThai)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValue("Đang phục vụ");

            entity.HasIndex(x => x.MaMon)
                .IsUnique();
        });


        modelBuilder.Entity<ChiTietThucDon>(entity =>
        {
            entity.ToTable("ChiTietThucDon");

            entity.HasKey(x => x.ChiTietThucDonID);

            entity.Property(x => x.SoThuTu)
                .IsRequired()
                .HasDefaultValue(1);

            entity.HasIndex(x => new
            {
                x.ThucDonID,
                x.MonAnID
            })
            .IsUnique();

            entity.HasIndex(x => new
            {
                x.ThucDonID,
                x.SoThuTu
            })
            .IsUnique();

            entity.HasOne(x => x.ThucDon)
                .WithMany(x => x.ChiTietThucDons)
                .HasForeignKey(x => x.ThucDonID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MonAn)
                .WithMany(x => x.ChiTietThucDons)
                .HasForeignKey(x => x.MonAnID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

}
