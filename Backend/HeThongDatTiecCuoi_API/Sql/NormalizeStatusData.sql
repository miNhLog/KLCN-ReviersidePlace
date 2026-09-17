USE RiversidePalaceDB;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE NguoiDung
SET TrangThai = 'ACTIVE'
WHERE TrangThai = N'Hoạt động';

UPDATE NguoiDung
SET TrangThai = 'SUSPENDED'
WHERE TrangThai = N'Tạm khóa';

UPDATE NguoiDung
SET TrangThai = 'INACTIVE'
WHERE TrangThai = N'Ngừng hoạt động';

UPDATE NhanVien
SET TrangThai = 'ACTIVE'
WHERE TrangThai = N'Đang làm việc';

UPDATE NhanVien
SET TrangThai = 'ON_LEAVE'
WHERE TrangThai = N'Tạm nghỉ';

UPDATE NhanVien
SET TrangThai = 'TERMINATED'
WHERE TrangThai = N'Đã nghỉ việc';

UPDATE SanhTiec
SET TrangThai = 'ACTIVE'
WHERE TrangThai = N'Hoạt động';

UPDATE SanhTiec
SET TrangThai = 'MAINTENANCE'
WHERE TrangThai = N'Bảo trì';

UPDATE SanhTiec
SET TrangThai = 'INACTIVE'
WHERE TrangThai = N'Ngừng hoạt động';

UPDATE LichSanh
SET TrangThai = 'AVAILABLE'
WHERE TrangThai = N'Trống';

UPDATE LichSanh
SET TrangThai = 'LOCKED'
WHERE TrangThai = N'Tạm khóa';

UPDATE LichSanh
SET TrangThai = 'BOOKED'
WHERE TrangThai = N'Đã đặt';

UPDATE ThucDon
SET TrangThai = 'ACTIVE'
WHERE TrangThai = N'Áp dụng';

UPDATE ThucDon
SET TrangThai = 'INACTIVE'
WHERE TrangThai = N'Ngừng áp dụng';

UPDATE MonAn
SET TrangThai = 'ACTIVE'
WHERE TrangThai = N'Đang phục vụ';

UPDATE MonAn
SET TrangThai = 'INACTIVE'
WHERE TrangThai = N'Ngừng phục vụ';

COMMIT TRANSACTION;
GO
