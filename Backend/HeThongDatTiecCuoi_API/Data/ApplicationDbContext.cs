using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<SanhTiec> SanhTiec => Set<SanhTiec>();
    public DbSet<VaiTro> VaiTro => Set<VaiTro>();
    public DbSet<NguoiDung> NguoiDung => Set<NguoiDung>();
    public DbSet<KhachHang> KhachHang => Set<KhachHang>();
    public DbSet<NhanVien> NhanVien => Set<NhanVien>();

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
            entity.Property(x => x.DiaChi).HasMaxLength(255);
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
