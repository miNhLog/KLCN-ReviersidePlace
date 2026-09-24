/*
    Nâng cấp database RiversidePalaceDB đang tồn tại để hỗ trợ:
    - Đăng nhập/đăng ký Google.
    - Tài khoản Google không bắt buộc có mật khẩu nội bộ.
    - Hồ sơ khách Google chưa bắt buộc có số điện thoại.

    Chạy script này một lần trên database hiện tại trước khi chạy phiên bản API mới.
*/

USE RiversidePalaceDB;
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

ALTER TABLE NguoiDung
    ALTER COLUMN MatKhauHash NVARCHAR(255) NULL;

IF EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE [name] = N'UQ_KhachHang_SoDienThoai'
      AND parent_object_id = OBJECT_ID(N'dbo.KhachHang')
)
BEGIN
    ALTER TABLE KhachHang DROP CONSTRAINT UQ_KhachHang_SoDienThoai;
END;

ALTER TABLE KhachHang
    ALTER COLUMN SoDienThoai NVARCHAR(20) NULL;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_KhachHang_SoDienThoai'
      AND object_id = OBJECT_ID(N'dbo.KhachHang')
)
BEGIN
    CREATE UNIQUE INDEX UX_KhachHang_SoDienThoai
        ON KhachHang(SoDienThoai)
        WHERE SoDienThoai IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.DangNhapNgoai', N'U') IS NULL
BEGIN
    CREATE TABLE DangNhapNgoai (
        DangNhapNgoaiID BIGINT IDENTITY(1,1) NOT NULL,
        NguoiDungID INT NOT NULL,
        NhaCungCap VARCHAR(30) NOT NULL,
        MaNguoiDungNhaCungCap NVARCHAR(255) NOT NULL,
        EmailNhaCungCap NVARCHAR(150) NULL,
        NgayLienKet DATETIME2(0) NOT NULL
            CONSTRAINT DF_DangNhapNgoai_NgayLienKet DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_DangNhapNgoai PRIMARY KEY (DangNhapNgoaiID),
        CONSTRAINT UQ_DangNhapNgoai_ProviderUser
            UNIQUE (NhaCungCap, MaNguoiDungNhaCungCap),
        CONSTRAINT UQ_DangNhapNgoai_UserProvider
            UNIQUE (NguoiDungID, NhaCungCap),
        CONSTRAINT FK_DangNhapNgoai_NguoiDung
            FOREIGN KEY (NguoiDungID)
            REFERENCES NguoiDung(NguoiDungID)
            ON DELETE CASCADE
    );
END;

COMMIT TRANSACTION;
GO

SELECT
    COL_NAME(OBJECT_ID(N'dbo.NguoiDung'), column_id) AS Cot,
    is_nullable AS ChoPhepNull
FROM sys.columns
WHERE object_id = OBJECT_ID(N'dbo.NguoiDung')
  AND [name] = N'MatKhauHash';

SELECT OBJECT_ID(N'dbo.DangNhapNgoai', N'U') AS DangNhapNgoaiObjectId;
GO
