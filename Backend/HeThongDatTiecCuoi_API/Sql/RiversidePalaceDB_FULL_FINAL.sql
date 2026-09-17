/*
    RiversidePalaceDB_FINAL_FIXED_STATUS.sql
    ============================================================================
    Database dong bo voi source update(1).zip

    - Chay 1 lan: Ctrl + A -> Execute.
    - TrangThaiID duoc gan co dinh theo tung nhom de INSERT gon va de doc.
    - Dung CONSTRAINT co ten cho PRIMARY KEY, FOREIGN KEY, UNIQUE, CHECK, DEFAULT.
    - Khong dung CREATE INDEX, CURSOR, MERGE, DECLARE hay dynamic SQL.
    - Script se XOA RiversidePalaceDB cu va tao lai tu dau.
    ============================================================================
*/

USE master;
GO

IF DB_ID(N'RiversidePalaceDB') IS NOT NULL
BEGIN
    ALTER DATABASE RiversidePalaceDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RiversidePalaceDB;
END
GO

CREATE DATABASE RiversidePalaceDB;
GO

USE RiversidePalaceDB;
GO

-- 1. VaiTro
CREATE TABLE VaiTro (
    VaiTroID INT IDENTITY(1,1) NOT NULL,
    TenVaiTro NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_VaiTro PRIMARY KEY (VaiTroID),
    CONSTRAINT UQ_VaiTro_TenVaiTro UNIQUE (TenVaiTro)
);
GO

-- 2. TrangThai
-- ID co dinh: 1xx Account, 2xx Employee, 3xx Hall, 4xx HallSchedule,
-- 5xx Menu, 6xx Dish, 7xx Process.
CREATE TABLE TrangThai (
    TrangThaiID INT NOT NULL,
    MaTrangThai VARCHAR(50) NOT NULL,
    TenTrangThai NVARCHAR(100) NOT NULL,
    NhomTrangThai VARCHAR(50) NOT NULL,
    MoTa NVARCHAR(500) NULL,
    CONSTRAINT PK_TrangThai PRIMARY KEY (TrangThaiID),
    CONSTRAINT UQ_TrangThai_Nhom_Ma UNIQUE (NhomTrangThai, MaTrangThai)
);
GO

-- 3. NguoiDung
CREATE TABLE NguoiDung (
    NguoiDungID INT IDENTITY(1,1) NOT NULL,
    VaiTroID INT NOT NULL,
    Email NVARCHAR(150) NOT NULL,
    MatKhauHash NVARCHAR(255) NOT NULL,
    TrangThaiID INT NOT NULL,
    NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_NguoiDung_NgayTao DEFAULT SYSDATETIME(),
    CONSTRAINT PK_NguoiDung PRIMARY KEY (NguoiDungID),
    CONSTRAINT UQ_NguoiDung_Email UNIQUE (Email),
    CONSTRAINT FK_NguoiDung_VaiTro FOREIGN KEY (VaiTroID) REFERENCES VaiTro(VaiTroID),
    CONSTRAINT FK_NguoiDung_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID)
);
GO

-- 4. TokenDatLaiMatKhau
CREATE TABLE TokenDatLaiMatKhau (
    TokenDatLaiMatKhauID BIGINT IDENTITY(1,1) NOT NULL,
    NguoiDungID INT NOT NULL,
    TokenHash CHAR(64) NOT NULL,
    HetHanLuc DATETIME2(0) NOT NULL,
    DaDungLuc DATETIME2(0) NULL,
    TaoLuc DATETIME2(0) NOT NULL,
    CONSTRAINT PK_TokenDatLaiMatKhau PRIMARY KEY (TokenDatLaiMatKhauID),
    CONSTRAINT UQ_TokenDatLaiMatKhau_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_TokenDatLaiMatKhau_NguoiDung FOREIGN KEY (NguoiDungID)
        REFERENCES NguoiDung(NguoiDungID) ON DELETE CASCADE
);
GO

-- 5. KhachHang
CREATE TABLE KhachHang (
    KhachHangID INT IDENTITY(1,1) NOT NULL,
    NguoiDungID INT NULL,
    HoTen NVARCHAR(150) NOT NULL,
    SoDienThoai NVARCHAR(20) NOT NULL,
    CONSTRAINT PK_KhachHang PRIMARY KEY (KhachHangID),
    CONSTRAINT UQ_KhachHang_SoDienThoai UNIQUE (SoDienThoai),
    CONSTRAINT FK_KhachHang_NguoiDung FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(NguoiDungID)
);
GO

-- 6. NhanVien
CREATE TABLE NhanVien (
    NhanVienID INT IDENTITY(1,1) NOT NULL,
    NguoiDungID INT NOT NULL,
    MaNhanVien NVARCHAR(50) NOT NULL,
    HoTen NVARCHAR(150) NOT NULL,
    SoDienThoai NVARCHAR(20) NULL,
    TrangThaiID INT NOT NULL,
    CONSTRAINT PK_NhanVien PRIMARY KEY (NhanVienID),
    CONSTRAINT UQ_NhanVien_NguoiDung UNIQUE (NguoiDungID),
    CONSTRAINT UQ_NhanVien_MaNhanVien UNIQUE (MaNhanVien),
    CONSTRAINT FK_NhanVien_NguoiDung FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(NguoiDungID),
    CONSTRAINT FK_NhanVien_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID)
);
GO

-- 7. SanhTiec
CREATE TABLE SanhTiec (
    SanhTiecID INT IDENTITY(1,1) NOT NULL,
    MaSanh NVARCHAR(50) NOT NULL,
    TenSanh NVARCHAR(150) NOT NULL,
    SucChuaToiThieu INT NULL,
    SucChuaToiDa INT NOT NULL,
    GiaThue DECIMAL(18,2) NOT NULL CONSTRAINT DF_SanhTiec_GiaThue DEFAULT 0,
    MoTa NVARCHAR(MAX) NULL,
    HinhAnh NVARCHAR(500) NULL,
    TrangThaiID INT NOT NULL,
    CONSTRAINT PK_SanhTiec PRIMARY KEY (SanhTiecID),
    CONSTRAINT UQ_SanhTiec_MaSanh UNIQUE (MaSanh),
    CONSTRAINT FK_SanhTiec_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT CK_SanhTiec_SucChuaToiDa CHECK (SucChuaToiDa > 0),
    CONSTRAINT CK_SanhTiec_SucChuaToiThieu CHECK (SucChuaToiThieu IS NULL OR (SucChuaToiThieu > 0 AND SucChuaToiThieu <= SucChuaToiDa)),
    CONSTRAINT CK_SanhTiec_GiaThue CHECK (GiaThue >= 0)
);
GO

-- 8. LichSanh
CREATE TABLE LichSanh (
    LichSanhID INT IDENTITY(1,1) NOT NULL,
    SanhTiecID INT NOT NULL,
    Ngay DATE NOT NULL,
    CaToChuc NVARCHAR(50) NOT NULL,
    TrangThaiID INT NOT NULL,
    GhiChu NVARCHAR(500) NULL,
    CONSTRAINT PK_LichSanh PRIMARY KEY (LichSanhID),
    CONSTRAINT UQ_LichSanh_Sanh_Ngay_Ca UNIQUE (SanhTiecID, Ngay, CaToChuc),
    CONSTRAINT FK_LichSanh_SanhTiec FOREIGN KEY (SanhTiecID) REFERENCES SanhTiec(SanhTiecID),
    CONSTRAINT FK_LichSanh_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT CK_LichSanh_CaToChuc CHECK (CaToChuc IN (N'Ca trưa', N'Ca tối'))
);
GO

-- 9. ThucDon
CREATE TABLE ThucDon (
    ThucDonID INT IDENTITY(1,1) NOT NULL,
    MaThucDon NVARCHAR(50) NOT NULL,
    TenThucDon NVARCHAR(150) NOT NULL,
    MoTa NVARCHAR(MAX) NULL,
    GiaMoiBan DECIMAL(18,2) NOT NULL,
    TrangThaiID INT NOT NULL,
    CONSTRAINT PK_ThucDon PRIMARY KEY (ThucDonID),
    CONSTRAINT UQ_ThucDon_MaThucDon UNIQUE (MaThucDon),
    CONSTRAINT FK_ThucDon_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID),
    CONSTRAINT CK_ThucDon_GiaMoiBan CHECK (GiaMoiBan >= 0)
);
GO

-- 10. MonAn
CREATE TABLE MonAn (
    MonAnID INT IDENTITY(1,1) NOT NULL,
    MaMon NVARCHAR(50) NOT NULL,
    TenMon NVARCHAR(150) NOT NULL,
    NhomMon NVARCHAR(100) NULL,
    HinhAnh NVARCHAR(500) NULL,
    TrangThaiID INT NOT NULL,
    CONSTRAINT PK_MonAn PRIMARY KEY (MonAnID),
    CONSTRAINT UQ_MonAn_MaMon UNIQUE (MaMon),
    CONSTRAINT FK_MonAn_TrangThai FOREIGN KEY (TrangThaiID) REFERENCES TrangThai(TrangThaiID)
);
GO

-- 11. ChiTietThucDon
CREATE TABLE ChiTietThucDon (
    ChiTietThucDonID INT IDENTITY(1,1) NOT NULL,
    ThucDonID INT NOT NULL,
    MonAnID INT NOT NULL,
    SoThuTu INT NOT NULL CONSTRAINT DF_ChiTietThucDon_SoThuTu DEFAULT 1,
    CONSTRAINT PK_ChiTietThucDon PRIMARY KEY (ChiTietThucDonID),
    CONSTRAINT UQ_ChiTietThucDon_ThucDon_MonAn UNIQUE (ThucDonID, MonAnID),
    CONSTRAINT UQ_ChiTietThucDon_ThucDon_SoThuTu UNIQUE (ThucDonID, SoThuTu),
    CONSTRAINT FK_ChiTietThucDon_ThucDon FOREIGN KEY (ThucDonID) REFERENCES ThucDon(ThucDonID),
    CONSTRAINT FK_ChiTietThucDon_MonAn FOREIGN KEY (MonAnID) REFERENCES MonAn(MonAnID),
    CONSTRAINT CK_ChiTietThucDon_SoThuTu CHECK (SoThuTu > 0)
);
GO

-- 12. GoiTrangTri
CREATE TABLE GoiTrangTri (
    GoiTrangTriID INT IDENTITY(1,1) NOT NULL,
    MaGoi NVARCHAR(50) NOT NULL,
    TenGoi NVARCHAR(150) NOT NULL,
    PhongCach NVARCHAR(100) NULL,
    MoTa NVARCHAR(MAX) NULL,
    Gia DECIMAL(18,2) NOT NULL,
    HinhAnh NVARCHAR(500) NULL,
    TrangThai NVARCHAR(50) NOT NULL CONSTRAINT DF_GoiTrangTri_TrangThai DEFAULT N'Áp dụng',
    CONSTRAINT PK_GoiTrangTri PRIMARY KEY (GoiTrangTriID),
    CONSTRAINT UQ_GoiTrangTri_MaGoi UNIQUE (MaGoi),
    CONSTRAINT CK_GoiTrangTri_Gia CHECK (Gia >= 0),
    CONSTRAINT CK_GoiTrangTri_TrangThai CHECK (TrangThai IN (N'Áp dụng', N'Ngừng áp dụng'))
);
GO

-- 13. DichVu
CREATE TABLE DichVu (
    DichVuID INT IDENTITY(1,1) NOT NULL,
    MaDichVu NVARCHAR(50) NOT NULL,
    TenDichVu NVARCHAR(150) NOT NULL,
    LoaiDichVu NVARCHAR(100) NULL,
    MoTa NVARCHAR(MAX) NULL,
    Gia DECIMAL(18,2) NOT NULL,
    HinhAnh NVARCHAR(500) NULL,
    TrangThai NVARCHAR(50) NOT NULL CONSTRAINT DF_DichVu_TrangThai DEFAULT N'Áp dụng',
    CONSTRAINT PK_DichVu PRIMARY KEY (DichVuID),
    CONSTRAINT UQ_DichVu_MaDichVu UNIQUE (MaDichVu),
    CONSTRAINT CK_DichVu_Gia CHECK (Gia >= 0),
    CONSTRAINT CK_DichVu_TrangThai CHECK (TrangThai IN (N'Áp dụng', N'Ngừng áp dụng'))
);
GO

-- 14. DatTiec
-- Source hien tai van map DatTiec.TrangThai dang chuoi, chua dung TrangThaiID.
CREATE TABLE DatTiec (
    DatTiecID INT IDENTITY(1,1) NOT NULL,
    MaDatTiec NVARCHAR(50) NOT NULL,
    KhachHangID INT NOT NULL,
    LichSanhID INT NOT NULL,
    ThucDonID INT NULL,
    GoiTrangTriID INT NULL,
    NhanVienTuVanID INT NULL,
    NganSachDuKien DECIMAL(18,2) NULL,
    PhongCachMongMuon NVARCHAR(100) NULL,
    SoLuongKhach INT NULL,
    SoBan INT NULL,
    GiaThucDonChot DECIMAL(18,2) NULL,
    GiaTrangTriChot DECIMAL(18,2) NULL,
    GiaSanhChot DECIMAL(18,2) NULL,
    TongTienDuKien DECIMAL(18,2) NULL,
    YeuCauDacBiet NVARCHAR(MAX) NULL,
    TrangThai NVARCHAR(50) NOT NULL CONSTRAINT DF_DatTiec_TrangThai DEFAULT N'Chờ xác nhận',
    LyDoHuy NVARCHAR(500) NULL,
    NgayDat DATETIME2(0) NOT NULL CONSTRAINT DF_DatTiec_NgayDat DEFAULT SYSDATETIME(),
    NgayCapNhat DATETIME2(0) NULL,
    CONSTRAINT PK_DatTiec PRIMARY KEY (DatTiecID),
    CONSTRAINT UQ_DatTiec_MaDatTiec UNIQUE (MaDatTiec),
    CONSTRAINT FK_DatTiec_KhachHang FOREIGN KEY (KhachHangID) REFERENCES KhachHang(KhachHangID),
    CONSTRAINT FK_DatTiec_LichSanh FOREIGN KEY (LichSanhID) REFERENCES LichSanh(LichSanhID),
    CONSTRAINT FK_DatTiec_ThucDon FOREIGN KEY (ThucDonID) REFERENCES ThucDon(ThucDonID),
    CONSTRAINT FK_DatTiec_GoiTrangTri FOREIGN KEY (GoiTrangTriID) REFERENCES GoiTrangTri(GoiTrangTriID),
    CONSTRAINT FK_DatTiec_NhanVienTuVan FOREIGN KEY (NhanVienTuVanID) REFERENCES NhanVien(NhanVienID),
    CONSTRAINT CK_DatTiec_TrangThai CHECK (TrangThai IN (N'Chờ xác nhận', N'Đã xác nhận', N'Đã cọc', N'Đang chuẩn bị', N'Hoàn tất', N'Đã hủy'))
);
GO

-- 15. DatTiec_DichVu
CREATE TABLE DatTiec_DichVu (
    DatTiecDichVuID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    DichVuID INT NOT NULL,
    SoLuong INT NOT NULL CONSTRAINT DF_DatTiec_DichVu_SoLuong DEFAULT 1,
    DonGiaChot DECIMAL(18,2) NOT NULL,
    GhiChu NVARCHAR(500) NULL,
    CONSTRAINT PK_DatTiec_DichVu PRIMARY KEY (DatTiecDichVuID),
    CONSTRAINT UQ_DatTiec_DichVu UNIQUE (DatTiecID, DichVuID),
    CONSTRAINT FK_DatTiec_DichVu_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT FK_DatTiec_DichVu_DichVu FOREIGN KEY (DichVuID) REFERENCES DichVu(DichVuID),
    CONSTRAINT CK_DatTiec_DichVu_SoLuong CHECK (SoLuong > 0),
    CONSTRAINT CK_DatTiec_DichVu_DonGia CHECK (DonGiaChot >= 0)
);
GO

-- 16. HopDong
CREATE TABLE HopDong (
    HopDongID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    MaHopDong NVARCHAR(50) NOT NULL,
    NgayLap DATE NOT NULL,
    TongGiaTri DECIMAL(18,2) NOT NULL,
    NoiDungHopDong NVARCHAR(MAX) NULL,
    DieuKhoanThanhToan NVARCHAR(MAX) NULL,
    TrangThai NVARCHAR(50) NOT NULL CONSTRAINT DF_HopDong_TrangThai DEFAULT N'Hiệu lực',
    CONSTRAINT PK_HopDong PRIMARY KEY (HopDongID),
    CONSTRAINT UQ_HopDong_DatTiec UNIQUE (DatTiecID),
    CONSTRAINT UQ_HopDong_MaHopDong UNIQUE (MaHopDong),
    CONSTRAINT FK_HopDong_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT CK_HopDong_TongGiaTri CHECK (TongGiaTri >= 0),
    CONSTRAINT CK_HopDong_TrangThai CHECK (TrangThai IN (N'Nháp', N'Hiệu lực', N'Hoàn tất', N'Đã hủy'))
);
GO

-- 17. ThanhToan
CREATE TABLE ThanhToan (
    ThanhToanID INT IDENTITY(1,1) NOT NULL,
    HopDongID INT NOT NULL,
    LoaiThanhToan NVARCHAR(50) NOT NULL,
    SoTien DECIMAL(18,2) NOT NULL,
    NgayThanhToan DATETIME2(0) NOT NULL CONSTRAINT DF_ThanhToan_NgayThanhToan DEFAULT SYSDATETIME(),
    PhuongThuc NVARCHAR(50) NULL,
    MaGiaoDich NVARCHAR(100) NULL,
    TrangThai NVARCHAR(50) NOT NULL CONSTRAINT DF_ThanhToan_TrangThai DEFAULT N'Chờ xác nhận',
    GhiChu NVARCHAR(500) NULL,
    CONSTRAINT PK_ThanhToan PRIMARY KEY (ThanhToanID),
    CONSTRAINT FK_ThanhToan_HopDong FOREIGN KEY (HopDongID) REFERENCES HopDong(HopDongID),
    CONSTRAINT CK_ThanhToan_Loai CHECK (LoaiThanhToan IN (N'Đặt cọc', N'Thanh toán đợt', N'Quyết toán')),
    CONSTRAINT CK_ThanhToan_SoTien CHECK (SoTien > 0),
    CONSTRAINT CK_ThanhToan_TrangThai CHECK (TrangThai IN (N'Chờ xác nhận', N'Thành công', N'Thất bại', N'Đã hủy'))
);
GO

-- 18. PhanCongDieuPhoi
CREATE TABLE PhanCongDieuPhoi (
    PhanCongID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    NhanVienID INT NOT NULL,
    VaiTroSuKien NVARCHAR(100) NOT NULL,
    TrangThai NVARCHAR(50) NOT NULL CONSTRAINT DF_PhanCongDieuPhoi_TrangThai DEFAULT N'Đã phân công',
    GhiChu NVARCHAR(255) NULL,
    NgayPhanCong DATETIME2(0) NOT NULL CONSTRAINT DF_PhanCongDieuPhoi_Ngay DEFAULT SYSDATETIME(),
    CONSTRAINT PK_PhanCongDieuPhoi PRIMARY KEY (PhanCongID),
    CONSTRAINT UQ_PhanCongDieuPhoi UNIQUE (DatTiecID, NhanVienID),
    CONSTRAINT FK_PhanCongDieuPhoi_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT FK_PhanCongDieuPhoi_NhanVien FOREIGN KEY (NhanVienID) REFERENCES NhanVien(NhanVienID),
    CONSTRAINT CK_PhanCongDieuPhoi_TrangThai CHECK (TrangThai IN (N'Đã phân công', N'Đã xác nhận', N'Hoàn tất', N'Đã hủy'))
);
GO

-- 19. SuCoTiec
CREATE TABLE SuCoTiec (
    SuCoID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    NhanVienBaoCaoID INT NULL,
    NhanVienXuLyID INT NULL,
    LoaiSuCo NVARCHAR(100) NOT NULL,
    MoTa NVARCHAR(MAX) NOT NULL,
    MucDo NVARCHAR(50) NOT NULL CONSTRAINT DF_SuCoTiec_MucDo DEFAULT N'Bình thường',
    TrangThai NVARCHAR(50) NOT NULL CONSTRAINT DF_SuCoTiec_TrangThai DEFAULT N'Chờ xử lý',
    HuongXuLy NVARCHAR(MAX) NULL,
    ThoiGianPhatSinh DATETIME2(0) NOT NULL CONSTRAINT DF_SuCoTiec_ThoiGianPhatSinh DEFAULT SYSDATETIME(),
    ThoiGianXuLy DATETIME2(0) NULL,
    CONSTRAINT PK_SuCoTiec PRIMARY KEY (SuCoID),
    CONSTRAINT FK_SuCoTiec_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT FK_SuCoTiec_NhanVienBaoCao FOREIGN KEY (NhanVienBaoCaoID) REFERENCES NhanVien(NhanVienID),
    CONSTRAINT FK_SuCoTiec_NhanVienXuLy FOREIGN KEY (NhanVienXuLyID) REFERENCES NhanVien(NhanVienID),
    CONSTRAINT CK_SuCoTiec_MucDo CHECK (MucDo IN (N'Thấp', N'Bình thường', N'Nghiêm trọng')),
    CONSTRAINT CK_SuCoTiec_TrangThai CHECK (TrangThai IN (N'Chờ xử lý', N'Đang xử lý', N'Đã xử lý', N'Đã đóng'))
);
GO

-- 20. ThayDoiLichTiec
CREATE TABLE ThayDoiLichTiec (
    ThayDoiLichID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    LichSanhCuID INT NOT NULL,
    LichSanhMoiID INT NOT NULL,
    LyDo NVARCHAR(500) NULL,
    TrangThai NVARCHAR(50) NOT NULL CONSTRAINT DF_ThayDoiLichTiec_TrangThai DEFAULT N'Chờ duyệt',
    NhanVienXuLyID INT NULL,
    NgayYeuCau DATETIME2(0) NOT NULL CONSTRAINT DF_ThayDoiLichTiec_NgayYeuCau DEFAULT SYSDATETIME(),
    NgayXuLy DATETIME2(0) NULL,
    CONSTRAINT PK_ThayDoiLichTiec PRIMARY KEY (ThayDoiLichID),
    CONSTRAINT FK_ThayDoiLichTiec_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT FK_ThayDoiLichTiec_LichCu FOREIGN KEY (LichSanhCuID) REFERENCES LichSanh(LichSanhID),
    CONSTRAINT FK_ThayDoiLichTiec_LichMoi FOREIGN KEY (LichSanhMoiID) REFERENCES LichSanh(LichSanhID),
    CONSTRAINT FK_ThayDoiLichTiec_NhanVien FOREIGN KEY (NhanVienXuLyID) REFERENCES NhanVien(NhanVienID),
    CONSTRAINT CK_ThayDoiLichTiec_KhacLich CHECK (LichSanhCuID <> LichSanhMoiID),
    CONSTRAINT CK_ThayDoiLichTiec_TrangThai CHECK (TrangThai IN (N'Chờ duyệt', N'Đã duyệt', N'Từ chối'))
);
GO

-- 21. MaQRDanhGia
CREATE TABLE MaQRDanhGia (
    MaQRDanhGiaID INT IDENTITY(1,1) NOT NULL,
    DatTiecID INT NOT NULL,
    MaQR NVARCHAR(150) NOT NULL,
    NgayTao DATETIME2(0) NOT NULL CONSTRAINT DF_MaQRDanhGia_NgayTao DEFAULT SYSDATETIME(),
    NgayHetHan DATETIME2(0) NULL,
    TrangThai NVARCHAR(50) NOT NULL CONSTRAINT DF_MaQRDanhGia_TrangThai DEFAULT N'Kích hoạt',
    CONSTRAINT PK_MaQRDanhGia PRIMARY KEY (MaQRDanhGiaID),
    CONSTRAINT UQ_MaQRDanhGia_DatTiec UNIQUE (DatTiecID),
    CONSTRAINT UQ_MaQRDanhGia_MaQR UNIQUE (MaQR),
    CONSTRAINT FK_MaQRDanhGia_DatTiec FOREIGN KEY (DatTiecID) REFERENCES DatTiec(DatTiecID),
    CONSTRAINT CK_MaQRDanhGia_TrangThai CHECK (TrangThai IN (N'Kích hoạt', N'Hết hạn', N'Khóa'))
);
GO

-- 22. DanhGia
CREATE TABLE DanhGia (
    DanhGiaID INT IDENTITY(1,1) NOT NULL,
    MaQRDanhGiaID INT NOT NULL,
    LoaiNguoiDanhGia NVARCHAR(50) NOT NULL,
    DiemSanh INT NULL,
    DiemMonAn INT NULL,
    DiemPhucVu INT NULL,
    DiemAmThanh INT NULL,
    DiemAnhSang INT NULL,
    DiemVeSinh INT NULL,
    DiemTongThe INT NOT NULL,
    BinhLuan NVARCHAR(MAX) NULL,
    MaPhienNguoiDung NVARCHAR(100) NOT NULL,
    NgayDanhGia DATETIME2(0) NOT NULL CONSTRAINT DF_DanhGia_NgayDanhGia DEFAULT SYSDATETIME(),
    CONSTRAINT PK_DanhGia PRIMARY KEY (DanhGiaID),
    CONSTRAINT UQ_DanhGia_QR_Phien UNIQUE (MaQRDanhGiaID, MaPhienNguoiDung),
    CONSTRAINT FK_DanhGia_MaQRDanhGia FOREIGN KEY (MaQRDanhGiaID) REFERENCES MaQRDanhGia(MaQRDanhGiaID),
    CONSTRAINT CK_DanhGia_LoaiNguoiDanhGia CHECK (LoaiNguoiDanhGia IN (N'Khách mời', N'Chủ tiệc')),
    CONSTRAINT CK_DanhGia_DiemSanh CHECK (DiemSanh IS NULL OR DiemSanh BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemMonAn CHECK (DiemMonAn IS NULL OR DiemMonAn BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemPhucVu CHECK (DiemPhucVu IS NULL OR DiemPhucVu BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemAmThanh CHECK (DiemAmThanh IS NULL OR DiemAmThanh BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemAnhSang CHECK (DiemAnhSang IS NULL OR DiemAnhSang BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemVeSinh CHECK (DiemVeSinh IS NULL OR DiemVeSinh BETWEEN 1 AND 5),
    CONSTRAINT CK_DanhGia_DiemTongThe CHECK (DiemTongThe BETWEEN 1 AND 5)
);
GO

-- SEED DATA

-- ==========================================================================
INSERT INTO VaiTro (TenVaiTro) VALUES
(N'Quản trị viên'),
(N'Nhân viên tư vấn'),
(N'Nhân viên điều phối'),
(N'Khách hàng');
GO

-- ============================================================================

-- ============================================================================
-- SEED TrangThai
-- ============================================================================
INSERT INTO TrangThai (TrangThaiID, NhomTrangThai, MaTrangThai, TenTrangThai, MoTa) VALUES
-- ACCOUNT: 100
(101, 'ACCOUNT',       'ACTIVE',       N'Hoạt động',         N'Tài khoản được phép đăng nhập'),
(102, 'ACCOUNT',       'INACTIVE',     N'Ngừng hoạt động',   N'Tài khoản ngừng hoạt động'),
(103, 'ACCOUNT',       'SUSPENDED',    N'Tạm khóa',          N'Tài khoản bị tạm khóa'),

-- EMPLOYEE: 200
(201, 'EMPLOYEE',      'ACTIVE',       N'Đang làm việc',     N'Nhân viên đang làm việc'),
(202, 'EMPLOYEE',      'ON_LEAVE',     N'Đang nghỉ phép',    N'Nhân viên đang nghỉ phép'),
(203, 'EMPLOYEE',      'TERMINATED',   N'Đã nghỉ việc',      N'Nhân viên đã nghỉ việc'),

-- HALL: 300
(301, 'HALL',          'ACTIVE',       N'Hoạt động',         N'Sảnh đang hoạt động'),
(302, 'HALL',          'INACTIVE',     N'Ngừng hoạt động',   N'Sảnh ngừng hoạt động'),
(303, 'HALL',          'MAINTENANCE',  N'Bảo trì',           N'Sảnh đang bảo trì'),

-- HALL_SCHEDULE: 400
(401, 'HALL_SCHEDULE', 'AVAILABLE',    N'Trống',             N'Ca tổ chức đang trống'),
(402, 'HALL_SCHEDULE', 'BOOKED',       N'Đã đặt',            N'Ca tổ chức đã được đặt'),
(403, 'HALL_SCHEDULE', 'LOCKED',       N'Tạm khóa',          N'Ca tổ chức bị tạm khóa'),

-- MENU: 500
(501, 'MENU',          'ACTIVE',       N'Đang áp dụng',      N'Thực đơn đang áp dụng'),
(502, 'MENU',          'INACTIVE',     N'Ngừng áp dụng',     N'Thực đơn ngừng áp dụng'),

-- DISH: 600
(601, 'DISH',          'ACTIVE',       N'Đang phục vụ',      N'Món ăn đang phục vụ'),
(602, 'DISH',          'INACTIVE',     N'Ngừng phục vụ',     N'Món ăn ngừng phục vụ'),

-- PROCESS: 700
(701, 'PROCESS',       'SUCCESS',      N'Thành công',        N'Tác vụ hoàn tất thành công'),
(702, 'PROCESS',       'FAILED',       N'Thất bại',          N'Tác vụ thất bại'),
(703, 'PROCESS',       'INITIALIZING', N'Đang khởi tạo',     N'Tác vụ đang được khởi tạo');
GO

-- 5. SanhTiec
-- Ten/suc chua/mo ta: du lieu cong khai.
-- ============================================================================

INSERT INTO SanhTiec (MaSanh, TenSanh, SucChuaToiThieu, SucChuaToiDa, GiaThue, MoTa, HinhAnh, TrangThaiID) VALUES
(N'S001', N'Amur', 300, 580, 30000000, N'Tinh tế và sang trọng, không gian ấm cúng và thanh lịch.', NULL, 301),
(N'S002', N'Elbe', 150, 360, 20000000, N'Hiện đại, trẻ trung, tinh giản và phóng khoáng.', NULL, 301),
(N'S003', N'Danube', 300, 540, 28000000, N'Huyền bí, lãng mạn với tông tím nhẹ và ánh sáng dịu.', NULL, 301),
(N'S004', N'Seine', NULL, 650, 32000000, N'Phong cách cổ điển châu Âu, không gian lãng mạn và thanh lịch.', NULL, 301),
(N'S005', N'Thames', 320, 580, 30000000, N'Phong cách hoàng gia, sang trọng và lộng lẫy.', NULL, 301),
(N'S006', N'Volga', 320, 580, 30000000, N'Không gian sang trọng, phù hợp tiệc cưới quy mô lớn.', NULL, 301),
(N'S007', N'Green Riverside', NULL, 300, 25000000, N'Không gian ngoài trời ven sông, gần gũi thiên nhiên.', NULL, 301),
(N'S008', N'Nile', 300, 650, 35000000, N'Không gian hiện đại, nghệ thuật và phù hợp phong cách cá nhân.', NULL, 301),
(N'S009', N'Grand Ballroom', 1150, 2000, 80000000, N'Đại sảnh quy mô lớn, không gian rộng và sang trọng.', NULL, 301);
GO


-- ============================================================================

-- 7. ThucDon
-- 24 set menu + gia moi ban tu du lieu cong khai.
-- ============================================================================

INSERT INTO ThucDon (MaThucDon, TenThucDon, MoTa, GiaMoiBan, TrangThaiID) VALUES
(N'TD001', N'Thực đơn tiệc cưới 1', N'Thực đơn kết hợp hải sản, bò Mỹ và sườn BBQ, mang phong vị Á - Nhật hiện đại.', 4350000, 501),
(N'TD002', N'Thực đơn tiệc cưới 2', N'Thực đơn nổi bật với cua, cá hồi, tôm, cá chẽm và sườn non, hương vị hài hòa dễ dùng.', 4350000, 501),
(N'TD003', N'Thực đơn tiệc cưới 3', N'Thực đơn kết hợp hải sản, bò sốt rượu vang và mì gà quay, mang phong cách Á - Âu.', 4350000, 501),

(N'TD004', N'Thực đơn tiệc cưới 4', N'Thực đơn thanh nhẹ với tôm, cá tầm, gà hấp và bò tiềm, kết hợp các nguyên liệu thảo mộc.', 4600000, 501),
(N'TD005', N'Thực đơn tiệc cưới 5', N'Thực đơn đa dạng với tempura, cá chẽm, vịt sốt cam gừng và lẩu miso bò Mỹ.', 4600000, 501),
(N'TD006', N'Thực đơn tiệc cưới 6', N'Thực đơn thiên về hải sản với mực, cua, cá chẽm, cá tầm kết hợp bò cuộn nấm.', 4600000, 501),

(N'TD007', N'Thực đơn tiệc cưới 7', N'Thực đơn đậm vị với cá chẽm, bò nướng, tôm rang muối, sườn mật ong và mì udon hải sản.', 4800000, 501),
(N'TD008', N'Thực đơn tiệc cưới 8', N'Thực đơn kết hợp tôm, ức ngỗng, gà tiềm, cá chẽm và sườn BBQ cùng lẩu cua đồng.', 4800000, 501),
(N'TD009', N'Thực đơn tiệc cưới 9', N'Thực đơn đậm đà với sườn mật ong, hải sản Tứ Xuyên, cá chẽm, vịt quay và lẩu gà.', 4800000, 501),

(N'TD010', N'Thực đơn tiệc cưới 10', N'Thực đơn kết hợp gà, tôm, bò, cá tầm và sườn BBQ, điểm nhấn là lẩu sa tế hải sản.', 5000000, 501),
(N'TD011', N'Thực đơn tiệc cưới 11', N'Thực đơn cao cấp với vịt quay, tôm, gà tiềm, cá chẽm, bò Mỹ và sườn non thảo mộc.', 5000000, 501),
(N'TD012', N'Thực đơn tiệc cưới 12', N'Thực đơn đậm vị với bò Mỹ, cá chẽm, tôm trứng muối, sườn BBQ và lẩu Thái tôm càng.', 5000000, 501),

(N'TD013', N'Thực đơn tiệc cưới 13', N'Thực đơn kết hợp tôm, nấm cuộn thịt, cá tầm, bò nấu đậu và lẩu Thái tôm càng.', 5200000, 501),
(N'TD014', N'Thực đơn tiệc cưới 14', N'Thực đơn hiện đại với cá chẽm, hải sản Tứ Xuyên, tôm càng, sườn sốt cà phê và mì bò.', 5200000, 501),
(N'TD015', N'Thực đơn tiệc cưới 15', N'Thực đơn gồm sườn sốt me, tôm trứng muối, gà hấp, cá bống mú và lẩu bò Mỹ cuộn nấm.', 5200000, 501),

(N'TD016', N'Thực đơn tiệc cưới 16', N'Thực đơn mang hương vị châu Á với bò Mỹ, tempura, cá bống mú, sườn cà ri và lẩu nấm.', 5500000, 501),
(N'TD017', N'Thực đơn tiệc cưới 17', N'Thực đơn kết hợp tôm, cá chẽm, sườn BBQ, vịt quay thảo mộc và lẩu cua đồng hải sản.', 5500000, 501),
(N'TD018', N'Thực đơn tiệc cưới 18', N'Thực đơn sang trọng với bò cuộn nấm, hải sản nhụy hoa nghệ tây, cá tầm và cơm rang tôm càng.', 5500000, 501),

(N'TD019', N'Thực đơn tiệc cưới 19', N'Thực đơn phong phú với bò Mỹ, cá hồi, tôm càng, gà hấp và cơm chiên xá xíu.', 5700000, 501),
(N'TD020', N'Thực đơn tiệc cưới 20', N'Thực đơn phong cách Á - Âu với tôm sốt dijon, gà chanh dây, cá tầm, bò sốt Thái và mì vịt quay.', 5700000, 501),
(N'TD021', N'Thực đơn tiệc cưới 21', N'Thực đơn cao cấp với tôm, mực, gà tiềm, cá chẽm, thăn bê và lẩu hải sản.', 5700000, 501),

(N'TD022', N'Thực đơn tiệc cưới 22', N'Thực đơn nổi bật với cá tầm, tôm, cua, vịt quay, bò sốt Thái và lẩu cá tầm măng chua.', 5900000, 501),
(N'TD023', N'Thực đơn tiệc cưới 23', N'Thực đơn hải sản đậm vị với tôm càng, cá bống mú, sườn BBQ và mì hấp vịt quay.', 5900000, 501),
(N'TD024', N'Thực đơn tiệc cưới 24', N'Thực đơn đa dạng với bò BBQ, cá hồi, gà tiềm, tôm càng, gân nai và lẩu cua đồng hải sản.', 5900000, 501);
GO

-- ============================================================================

-- 8. MonAn
-- Ten mon: du lieu cong khai. 
-- ============================================================================

INSERT INTO MonAn (MaMon, TenMon, NhomMon, HinhAnh, TrangThaiID) VALUES
(N'MA001', N'Tôm phủ bánh ngô sốt phô mai cay', N'Món khai vị', NULL, 601),
(N'MA002', N'Salad ức ngỗng xông khói', N'Món khai vị', NULL, 601),
(N'MA003', N'Súp dumpling mực', N'Món súp', NULL, 601),
(N'MA004', N'Cá chẽm đút lò sốt Yakiniku', N'Món chính', NULL, 601),
(N'MA005', N'Sườn non BBQ và khoai tây', N'Món chính', NULL, 601),
(N'MA006', N'Lẩu miso bò Mỹ dùng với mì somen', N'Món chính', NULL, 601),
(N'MA007', N'Pudding trà xanh', N'Món tráng miệng', NULL, 601),
(N'MA008', N'Chả giò cua cá hồi', N'Món khai vị', NULL, 601),
(N'MA009', N'Cá chẽm đút lò sốt phô mai', N'Món khai vị', NULL, 601),
(N'MA010', N'Súp bắp cua măng tây', N'Món súp', NULL, 601),
(N'MA011', N'Tôm phong sa dùng với salad', N'Món chính', NULL, 601),
(N'MA012', N'Sườn non nấu đậu dùng với bánh mì', N'Món chính', NULL, 601),
(N'MA013', N'Lẩu nấm sườn non dùng với mì udon', N'Món chính', NULL, 601),
(N'MA014', N'Bánh mousse dừa', N'Món tráng miệng', NULL, 601),
(N'MA015', N'Mực rang muối dùng với salad xoài', N'Món khai vị', NULL, 601),
(N'MA016', N'Tôm cuộn cua Nhật BBQ', N'Món khai vị', NULL, 601),
(N'MA017', N'Súp sườn hạnh nhân', N'Món súp', NULL, 601),
(N'MA018', N'Cá chẽm sốt Singapore', N'Món chính', NULL, 601),
(N'MA019', N'Bắp bò sốt rượu vang đỏ dùng với bánh mì', N'Món chính', NULL, 601),
(N'MA020', N'Mì gà quay', N'Món chính', NULL, 601),
(N'MA021', N'Bánh trứng pudding Nhật', N'Món tráng miệng', NULL, 601),
(N'MA022', N'Chả giò bách hoa sốt chua ngọt', N'Món khai vị', NULL, 601),
(N'MA023', N'Tôm cuộn cua Nhật sốt BBQ', N'Món khai vị', NULL, 601),
(N'MA024', N'Bắp bò tiềm đông trùng thảo', N'Món súp', NULL, 601),
(N'MA025', N'Cá tầm sốt cam', N'Món chính', NULL, 601),
(N'MA026', N'Gà ta hấp bạch linh và cải thìa', N'Món chính', NULL, 601),
(N'MA027', N'Mì xá xíu hoa kim ngân', N'Món chính', NULL, 601),
(N'MA028', N'Chè đậu đỏ vị gừng tươi', N'Món tráng miệng', NULL, 601),
(N'MA029', N'Tôm tempura - salad mận', N'Món khai vị', NULL, 601),
(N'MA030', N'Sườn quay mật ong', N'Món khai vị', NULL, 601),
(N'MA031', N'Ức vịt sốt cam gừng - khoai tây', N'Món chính', NULL, 601),
(N'MA032', N'Lẩu miso bò Mỹ dùng với mì udon', N'Món chính', NULL, 601),
(N'MA033', N'Mực rang muối Hồng Kông', N'Món khai vị', NULL, 601),
(N'MA034', N'Salad gà rong biển', N'Món khai vị', NULL, 601),
(N'MA035', N'Soup miso thịt cua rong biển', N'Món súp', NULL, 601),
(N'MA036', N'Cá chẽm hấp hành gừng', N'Món chính', NULL, 601),
(N'MA037', N'Bò cuộn nấm sốt tiêu và khoai tây', N'Món chính', NULL, 601),
(N'MA038', N'Lẩu cá tầm măng chua dùng với bún tươi', N'Món chính', NULL, 601),
(N'MA039', N'Bánh kem dừa', N'Món tráng miệng', NULL, 601),
(N'MA040', N'Cá chẽm cuộn thịt sốt phô mai', N'Món khai vị', NULL, 601),
(N'MA041', N'Bò nướng - salad xoài', N'Món khai vị', NULL, 601),
(N'MA042', N'Súp miso gà', N'Món súp', NULL, 601),
(N'MA043', N'Tôm rang muối tỏi', N'Món chính', NULL, 601),
(N'MA044', N'Sườn non mật ong và khoai lang nghiền', N'Món chính', NULL, 601),
(N'MA045', N'Mì udon hải sản sốt miso tiêu đen', N'Món chính', NULL, 601),
(N'MA046', N'Bánh kem dừa - sầu riêng', N'Món tráng miệng', NULL, 601),
(N'MA047', N'Tôm phủ bánh ngô chiên giòn', N'Món khai vị', NULL, 601),
(N'MA048', N'Gà tiềm sâm Hàn Quốc', N'Món súp', NULL, 601),
(N'MA049', N'Cá chẽm sốt X.O - bông cải', N'Món chính', NULL, 601),
(N'MA050', N'Sườn BBQ dùng với khoai tây', N'Món chính', NULL, 601),
(N'MA051', N'Lẩu cua đồng hải sản dùng với bún tươi', N'Món chính', NULL, 601),
(N'MA052', N'Trái cây tươi - Cam nho', N'Món tráng miệng', NULL, 601),
(N'MA053', N'Chả tôm hạnh nhân', N'Món khai vị', NULL, 601),
(N'MA054', N'Súp hải sản Tứ Xuyên', N'Món súp', NULL, 601),
(N'MA055', N'Vịt quay sốt cam thảo', N'Món chính', NULL, 601),
(N'MA056', N'Lẩu gà nấu ớt xiêm xanh dùng với bún tươi', N'Món chính', NULL, 601),
(N'MA057', N'Chè tàu hủ trái vải', N'Món tráng miệng', NULL, 601),
(N'MA058', N'Súp bò Mỹ nấu nấm', N'Món súp', NULL, 601),
(N'MA059', N'Cá tầm hấp jambon', N'Món chính', NULL, 601),
(N'MA060', N'Sườn heo BBQ dùng với bánh bao', N'Món chính', NULL, 601),
(N'MA061', N'Lẩu sa tế hải sản dùng với bún tươi', N'Món chính', NULL, 601),
(N'MA062', N'Bánh kem dâu tây', N'Món tráng miệng', NULL, 601),
(N'MA063', N'Salad xoài - vịt quay', N'Món khai vị', NULL, 601),
(N'MA064', N'Chả tôm lăn cốm', N'Món khai vị', NULL, 601),
(N'MA065', N'Gà tiềm đông trùng thảo', N'Món súp', NULL, 601),
(N'MA066', N'Cá chẽm hấp jambon', N'Món chính', NULL, 601),
(N'MA067', N'Bắp bò Mỹ sốt miso nấm', N'Món chính', NULL, 601),
(N'MA068', N'Sườn non tiềm thảo mộc dùng với mì tươi', N'Món chính', NULL, 601),
(N'MA069', N'Bánh chocolate', N'Món tráng miệng', NULL, 601),
(N'MA070', N'Bò Mỹ nướng mắm nhĩ và salad xoài', N'Món khai vị', NULL, 601),
(N'MA071', N'Cá chẽm cuộn thịt BBQ', N'Món khai vị', NULL, 601),
(N'MA072', N'Súp dumpling hải sản', N'Món súp', NULL, 601),
(N'MA073', N'Tôm rang trứng muối và salad táo', N'Món chính', NULL, 601),
(N'MA074', N'Sườn non nướng BBQ dùng với khoai tây tỏi', N'Món chính', NULL, 601),
(N'MA075', N'Lẩu Thái tôm càng baby dùng với bún tươi', N'Món chính', NULL, 601),
(N'MA076', N'Chè hạt sen nhãn nhục', N'Món tráng miệng', NULL, 601),
(N'MA077', N'Nấm cuộn thịt sốt Teriyaki', N'Món khai vị', NULL, 601),
(N'MA078', N'Súp hoành thánh hải sản', N'Món súp', NULL, 601),
(N'MA079', N'Cá tầm sốt Singapore', N'Món chính', NULL, 601),
(N'MA080', N'Bắp bò nấu đậu răng ngựa dùng với bánh mì', N'Món chính', NULL, 601),
(N'MA081', N'Lẩu Thái tôm càng baby dùng với bún', N'Món chính', NULL, 601),
(N'MA082', N'Bánh tiramisu', N'Món tráng miệng', NULL, 601),
(N'MA083', N'Cá chẽm cuộn thịt sốt miso chanh dây', N'Món khai vị', NULL, 601),
(N'MA084', N'Gỏi gà rong biển', N'Món khai vị', NULL, 601),
(N'MA085', N'Tôm càng sốt giấm đen', N'Món chính', NULL, 601),
(N'MA086', N'Sườn non sốt cà phê - khoai môn chiên giòn', N'Món chính', NULL, 601),
(N'MA087', N'Mì Phúc Kiến - bò hầm tiêu đen', N'Món chính', NULL, 601),
(N'MA088', N'Pudding quả mọng', N'Món tráng miệng', NULL, 601),
(N'MA089', N'Sườn non sốt me Thái', N'Món khai vị', NULL, 601),
(N'MA090', N'Tôm rang trứng muối - salad táo xanh', N'Món khai vị', NULL, 601),
(N'MA091', N'Súp dumpling tôm', N'Món súp', NULL, 601),
(N'MA092', N'Gà hấp đông trùng thảo dùng với cải Hồng Kông', N'Món chính', NULL, 601),
(N'MA093', N'Cá bống mú hấp Hồng Kông', N'Món chính', NULL, 601),
(N'MA094', N'Lẩu bò Mỹ cuộn nấm dùng với mì somen', N'Món chính', NULL, 601),
(N'MA095', N'Chè tàu hủ hạnh nhân', N'Món tráng miệng', NULL, 601),
(N'MA096', N'Bò Mỹ thượng hạng rang muối tỏi', N'Món khai vị', NULL, 601),
(N'MA097', N'Salad Nhật - tôm tempura', N'Món khai vị', NULL, 601),
(N'MA098', N'Súp hải sản hương vị Thái', N'Món súp', NULL, 601),
(N'MA099', N'Cá bống mú sốt Singapore', N'Món chính', NULL, 601),
(N'MA100', N'Sườn nướng cà ri - khoai môn chiên', N'Món chính', NULL, 601),
(N'MA101', N'Tôm cuộn cua Nhật sốt cay', N'Món khai vị', NULL, 601),
(N'MA102', N'Cá chẽm chiên nước mắm xoài', N'Món khai vị', NULL, 601),
(N'MA103', N'Súp gà tiềm đông trùng thảo', N'Món súp', NULL, 601),
(N'MA104', N'Sườn non nướng BBQ dùng với bánh bao', N'Món chính', NULL, 601),
(N'MA105', N'Vịt quay thảo mộc', N'Món chính', NULL, 601),
(N'MA106', N'Lẩu cua đồng hải sản dùng với bún', N'Món chính', NULL, 601),
(N'MA107', N'Bánh opera', N'Món tráng miệng', NULL, 601),
(N'MA108', N'Bò Mỹ cuộn nấm BBQ', N'Món khai vị', NULL, 601),
(N'MA109', N'Súp hải sản với nhụy hoa nghệ tây', N'Món súp', NULL, 601),
(N'MA110', N'Cá tầm sốt miso chanh dây', N'Món chính', NULL, 601),
(N'MA111', N'Giò heo tiềm jambon', N'Món chính', NULL, 601),
(N'MA112', N'Cơm rang tôm càng baby', N'Món chính', NULL, 601),
(N'MA113', N'Bò Mỹ cuộn xúc xích sốt đậu pois', N'Món khai vị', NULL, 601),
(N'MA114', N'Gỏi xoài cá hồi', N'Món khai vị', NULL, 601),
(N'MA115', N'Tôm càng baby rang muối tỏi', N'Món chính', NULL, 601),
(N'MA116', N'Gà hấp jambon và cải Hồng Kông', N'Món chính', NULL, 601),
(N'MA117', N'Cơm chiên xá xíu - bông cải xanh', N'Món chính', NULL, 601),
(N'MA118', N'Chè tàu hủ long nhãn', N'Món tráng miệng', NULL, 601),
(N'MA119', N'Tôm bách hoa sốt dijon trứng muối', N'Món khai vị', NULL, 601),
(N'MA120', N'Gà tẩm gia vị chiên giòn sốt chanh dây', N'Món khai vị', NULL, 601),
(N'MA121', N'Súp miso hải sản', N'Món súp', NULL, 601),
(N'MA122', N'Cá tầm phi lê hấp cải Hồng Kông', N'Món chính', NULL, 601),
(N'MA123', N'Bắp bò sốt Thái dùng với bánh mì nâu', N'Món chính', NULL, 601),
(N'MA124', N'Mì hấp vịt quay thảo mộc', N'Món chính', NULL, 601),
(N'MA125', N'Mực nhồi trứng muối - salad', N'Món khai vị', NULL, 601),
(N'MA126', N'Thăn bê nướng BBQ - khoai tây tỏi', N'Món chính', NULL, 601),
(N'MA127', N'Lẩu lê hải sản dùng với bún tươi', N'Món chính', NULL, 601),
(N'MA128', N'Bánh choco fondant', N'Món tráng miệng', NULL, 601),
(N'MA129', N'Cá tầm sốt me Thái và salad', N'Món khai vị', NULL, 601),
(N'MA130', N'Súp gạch cua bắp non', N'Món súp', NULL, 601),
(N'MA131', N'Vịt quay thảo mộc - bánh bao hấp', N'Món chính', NULL, 601),
(N'MA132', N'Sườn non BBQ dùng với bánh bao', N'Món chính', NULL, 601),
(N'MA133', N'Mì hấp vịt quay', N'Món chính', NULL, 601),
(N'MA134', N'Bánh kem chanh dây', N'Món tráng miệng', NULL, 601),
(N'MA135', N'Bò cuộn xúc xích sốt BBQ', N'Món khai vị', NULL, 601),
(N'MA136', N'Salad cá hồi sốt mè', N'Món khai vị', NULL, 601),
(N'MA137', N'Cánh gà tiềm sâm Hàn Quốc', N'Món súp', NULL, 601),
(N'MA138', N'Gân nai sốt X.O và cải thìa', N'Món chính', NULL, 601);
GO

-- ============================================================================

-- 9. ChiTietThucDon
-- ============================================================================

INSERT INTO ChiTietThucDon (ThucDonID, MonAnID, SoThuTu) VALUES
(1, 1, 1),
(1, 2, 2),
(1, 3, 3),
(1, 4, 4),
(1, 5, 5),
(1, 6, 6),
(1, 7, 7),
(2, 8, 1),
(2, 9, 2),
(2, 10, 3),
(2, 11, 4),
(2, 12, 5),
(2, 13, 6),
(2, 14, 7),
(3, 15, 1),
(3, 16, 2),
(3, 17, 3),
(3, 18, 4),
(3, 19, 5),
(3, 20, 6),
(3, 21, 7),
(4, 22, 1),
(4, 23, 2),
(4, 24, 3),
(4, 25, 4),
(4, 26, 5),
(4, 27, 6),
(4, 28, 7),
(5, 29, 1),
(5, 30, 2),
(5, 10, 3),
(5, 18, 4),
(5, 31, 5),
(5, 32, 6),
(5, 7, 7),
(6, 33, 1),
(6, 34, 2),
(6, 35, 3),
(6, 36, 4),
(6, 37, 5),
(6, 38, 6),
(6, 39, 7),
(7, 40, 1),
(7, 41, 2),
(7, 42, 3),
(7, 43, 4),
(7, 44, 5),
(7, 45, 6),
(7, 46, 7),
(8, 47, 1),
(8, 2, 2),
(8, 48, 3),
(8, 49, 4),
(8, 50, 5),
(8, 51, 6),
(8, 52, 7),
(9, 30, 1),
(9, 53, 2),
(9, 54, 3),
(9, 18, 4),
(9, 55, 5),
(9, 56, 6),
(9, 57, 7),
(10, 34, 1),
(10, 1, 2),
(10, 58, 3),
(10, 59, 4),
(10, 60, 5),
(10, 61, 6),
(10, 62, 7),
(11, 63, 1),
(11, 64, 2),
(11, 65, 3),
(11, 66, 4),
(11, 67, 5),
(11, 68, 6),
(11, 69, 7),
(12, 70, 1),
(12, 71, 2),
(12, 72, 3),
(12, 73, 4),
(12, 74, 5),
(12, 75, 6),
(12, 76, 7),
(13, 47, 1),
(13, 77, 2),
(13, 78, 3),
(13, 79, 4),
(13, 80, 5),
(13, 81, 6),
(13, 82, 7),
(14, 83, 1),
(14, 84, 2),
(14, 54, 3),
(14, 85, 4),
(14, 86, 5),
(14, 87, 6),
(14, 88, 7),
(15, 89, 1),
(15, 90, 2),
(15, 91, 3),
(15, 92, 4),
(15, 93, 5),
(15, 94, 6),
(15, 95, 7),
(16, 96, 1),
(16, 97, 2),
(16, 98, 3),
(16, 99, 4),
(16, 100, 5),
(16, 13, 6),
(16, 28, 7),
(17, 101, 1),
(17, 102, 2),
(17, 103, 3),
(17, 104, 4),
(17, 105, 5),
(17, 106, 6),
(17, 107, 7),
(18, 84, 1),
(18, 108, 2),
(18, 109, 3),
(18, 110, 4),
(18, 111, 5),
(18, 112, 6),
(18, 76, 7),
(19, 113, 1),
(19, 114, 2),
(19, 72, 3),
(19, 115, 4),
(19, 116, 5),
(19, 117, 6),
(19, 118, 7),
(20, 119, 1),
(20, 120, 2),
(20, 121, 3),
(20, 122, 4),
(20, 123, 5),
(20, 124, 6),
(20, 107, 7),
(21, 23, 1),
(21, 125, 2),
(21, 65, 3),
(21, 18, 4),
(21, 126, 5),
(21, 127, 6),
(21, 128, 7),
(22, 129, 1),
(22, 23, 2),
(22, 130, 3),
(22, 131, 4),
(22, 123, 5),
(22, 38, 6),
(22, 118, 7),
(23, 115, 1),
(23, 84, 2),
(23, 72, 3),
(23, 99, 4),
(23, 132, 5),
(23, 133, 6),
(23, 134, 7),
(24, 135, 1),
(24, 136, 2),
(24, 137, 3),
(24, 115, 4),
(24, 138, 5),
(24, 51, 6),
(24, 57, 7);
GO

-- ============================================================================

-- 10. GoiTrangTri
-- Ten/mo ta: du lieu cong khai. Gia: MO PHONG DEV.
-- ===========================================================================
INSERT INTO GoiTrangTri (MaGoi, TenGoi, PhongCach, MoTa, Gia, HinhAnh, TrangThai) VALUES
(N'GT001', N'Endless Love', N'Lãng mạn', N'Chủ đề tình yêu vĩnh cửu, không gian cưới lãng mạn.', 20000000, NULL, N'Áp dụng'),
(N'GT002', N'Đám cưới cuối năm', N'Nhẹ nhàng', N'Trang trí nhẹ nhàng, không quá cầu kỳ.', 18000000, NULL, N'Áp dụng'),
(N'GT003', N'Elegant Blue', N'Hiện đại / lãng mạn', N'Tông xanh dương chủ đạo, thanh lịch.', 22000000, NULL, N'Áp dụng'),
(N'GT004', N'The Color of Love', N'Sân vườn / lãng mạn', N'Không gian sân vườn với hoa nhiều màu sắc.', 25000000, NULL, N'Áp dụng'),
(N'GT005', N'Dear My Princess', N'Cổ tích / lãng mạn', N'Phong cách công chúa, khu vườn và cảm giác cổ tích.', 28000000, NULL, N'Áp dụng'),
(N'GT006', N'Tiệc cưới ven sông', N'Thiên nhiên / ngoài trời', N'Không gian ngoài trời ven sông, thoáng và thanh bình.', 22000000, NULL, N'Áp dụng'),
(N'GT007', N'Tiệc cưới mùa thu', N'Lãng mạn', N'Phối tông xanh dương, trắng, hồng và xanh lam.', 24000000, NULL, N'Áp dụng');
GO

-- ============================================================================

-- 11. DichVu
-- Ten/mo ta: du lieu cong khai. Gia: MO PHONG DEV.
-- ============================================================================
INSERT INTO DichVu (MaDichVu, TenDichVu, LoaiDichVu, MoTa, Gia, HinhAnh, TrangThai) VALUES
(N'DV001', N'MC tiệc cưới', N'Chương trình', N'Dịch vụ MC được Riverside Palace nêu trong gói tổ chức tiệc cưới.', 5000000, NULL, N'Áp dụng'),
(N'DV002', N'Âm thanh - ánh sáng', N'Kỹ thuật', N'Hệ thống âm thanh và ánh sáng phục vụ tiệc cưới.', 12000000, NULL, N'Áp dụng'),
(N'DV003', N'Sân khấu', N'Kỹ thuật / tổ chức', N'Hạng mục sân khấu trong dịch vụ tổ chức tiệc cưới.', 8000000, NULL, N'Áp dụng'),
(N'DV004', N'Tiết mục mở màn', N'Giải trí', N'Tiết mục mở màn được nhắc trong dịch vụ tiệc cưới trọn gói.', 7000000, NULL, N'Áp dụng'),
(N'DV005', N'Lễ tân', N'Phục vụ', N'Dịch vụ lễ tân được nêu trong gói tổ chức tiệc cưới.', 3000000, NULL, N'Áp dụng');
GO

-- ============================================================================
-- SEED tai khoan Admin
-- ============================================================================

INSERT INTO NguoiDung (VaiTroID, Email, MatKhauHash, TrangThaiID, NgayTao) VALUES
((SELECT VaiTroID FROM VaiTro WHERE TenVaiTro = N'Quản trị viên'), N'adminriverside@gmail.com', N'AQAAAAIAAYagAAAAEMLXqqxpcc3Ydg1JgAL92fbuGs44ame+FFih44Ifc3+HlDrvTBatGmudGFhDjcraXw==', 101, GETDATE());
GO

-- ==========================================================================
-- KIEM TRA NHANH
-- ==========================================================================
SELECT COUNT(*) AS SoVaiTro FROM VaiTro;
SELECT COUNT(*) AS SoTrangThai FROM TrangThai;
SELECT COUNT(*) AS SoSanh FROM SanhTiec;
SELECT COUNT(*) AS SoThucDon FROM ThucDon;
SELECT COUNT(*) AS SoMonAn FROM MonAn;
SELECT COUNT(*) AS SoChiTietThucDon FROM ChiTietThucDon;
SELECT COUNT(*) AS SoNguoiDung FROM NguoiDung;
GO

PRINT N'Da tao RiversidePalaceDB thanh cong.';
PRINT N'Schema gom 20 bang nghiep vu + TrangThai + TokenDatLaiMatKhau.';
PRINT N'Source hien tai dung TrangThaiID cho Account, Employee, Hall, HallSchedule, Menu, Dish.';
GO
