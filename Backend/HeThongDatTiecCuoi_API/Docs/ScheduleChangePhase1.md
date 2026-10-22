# Thay đổi lịch tiệc — Backend giai đoạn 1

## Cài đặt database

Chạy `Sql/Updates/20261022_ScheduleChangePhase1.sql` trên database hiện hữu. Script chỉ cập nhật cộng thêm, chạy trong transaction, có thể chạy lại và không `DROP/CREATE DATABASE`. Script cũng bổ sung phần schema PayOS tối thiểu nếu database cũ chưa chạy `Add_PayOS_Payment_Tables.sql`.

POL-CHG-05 phiên bản 1 được seed với các mức phí chính thức:

- 0–6 ngày: 5.000.000 VNĐ, chỉ Hall Manager được tạo ngoại lệ.
- 7–29 ngày: 5.000.000 VNĐ.
- 30–44 ngày: 2.000.000 VNĐ.
- Từ 45 ngày: 0 VNĐ.
- Ngày mới tối thiểu 14 ngày; thanh toán 15 phút; giữ lịch 20 phút.

Thời gian nghiệp vụ dùng ngày địa phương `Asia/Ho_Chi_Minh` (`SE Asia Standard Time` trên Windows).

## Workflow

`PENDING → UNDER_REVIEW → AWAITING_QUOTE_ACCEPTANCE → PAYMENT_PENDING → APPROVED`.

Các trạng thái kết thúc/ngoại lệ: `REJECTED`, `CANCELLED`, `EXPIRED`, `RECONCILIATION_REQUIRED`. `APPROVED` chỉ được ghi sau khi booking, lịch cũ/lịch mới, hợp đồng/phụ lục và giữ chỗ đã được cập nhật thành công. Nếu báo giá không có khoản phải thu, bước PayOS được bỏ qua nhưng khách vẫn phải xác nhận báo giá bằng OTP riêng.

## API

Public, không cần tài khoản:

- `POST /api/v1/public/schedule-changes/eligibility`
- `POST /api/v1/public/schedule-changes/availability`
- `POST /api/v1/public/schedule-changes/otp/send`
- `POST /api/v1/public/schedule-changes/otp/verify`
- `POST /api/v1/public/schedule-changes`
- `POST /api/v1/public/schedule-changes/lookup`
- `POST /api/v1/public/schedule-changes/{id}/detail`
- `POST /api/v1/public/schedule-changes/{id}/quote/accept`
- `POST /api/v1/public/schedule-changes/{id}/withdraw`
- `POST /api/v1/public/schedule-changes/{id}/payment/status`

Hall Manager (`Quản lý sảnh`, giới hạn theo sảnh hiện đang phụ trách):

- `GET /api/hall-manager/schedule-changes`
- `GET /api/hall-manager/schedule-changes/{id}`
- `POST /api/hall-manager/schedule-changes`
- `POST /api/hall-manager/schedule-changes/{id}/review`

Manager:

- `GET /api/manager/schedule-changes`
- `GET /api/manager/schedule-changes/{id}`
- `POST /api/manager/schedule-changes/{id}/quote`
- `POST /api/manager/schedule-changes/{id}/reject`
- `POST /api/manager/schedule-changes/{id}/reconcile`
- `GET /api/manager/schedule-changes/{id}/financials`
- `POST /api/manager/schedule-changes/{id}/refunds/{refundId}/confirm`

Policy (`Quản trị viên` hoặc `Quản lý`):

- `GET /api/admin/schedule-change-policy/active`
- `GET /api/admin/schedule-change-policy/history`
- `POST /api/admin/schedule-change-policy/versions`

Webhook PayOS tiếp tục dùng `POST /api/v1/payment/payos-webhook`; dispatcher phân biệt `DEPOSIT` và `SCHEDULE_CHANGE`, không gọi logic hoàn tất tiền cọc cho đổi lịch.

## SMS

Production không tự động dùng fake provider. Cấu hình:

```json
"Sms": {
  "Enabled": true,
  "Provider": "Http",
  "Endpoint": "https://provider.example/messages",
  "ApiKey": "set-with-secret-store",
  "ApiKeyHeader": "X-Api-Key",
  "SenderId": "Riverside"
}
```

Adapter gửi JSON gồm `to`, `sender`, `message`. Nếu nhà cung cấp dùng contract khác, thay adapter `ISmsSender` mà không đổi nghiệp vụ OTP. Development/Test có thể đặt `Provider=Development`, `Enabled=true`, `DevelopmentCode` là mã 6 chữ số qua user-secrets hoặc biến môi trường. Mã không được trả về API hoặc ghi log.

## PayOS

Ngoài cấu hình hiện có, đặt:

- `PayOS:ScheduleChangeReturnUrl`
- `PayOS:ScheduleChangeCancelUrl`

`IsDemoMode` chỉ scale `SoTienGiaoDich`; báo giá, `SoTienGhiNhan`, hợp đồng, công nợ và hoàn tiền luôn dùng số tiền nghiệp vụ đầy đủ. Chưa có API hoàn tiền PayOS phù hợp nên hệ thống chỉ tạo bản ghi `PENDING_MANUAL` kèm audit/bằng chứng; không giả lập đã hoàn tiền.

## Bảo đảm đồng thời và dữ liệu

- Unique filtered index ngăn hai yêu cầu mở cho cùng booking.
- Unique filtered index ngăn hai giữ chỗ `ACTIVE` cho cùng lịch sảnh.
- `OrderCode` unique; một nghĩa vụ đổi lịch chỉ có một giao dịch `PENDING/PAID`.
- Webhook lặp và polling đồng thời kiểm tra trạng thái transaction trước khi ghi nhận tiền.
- Booking hiện hữu và API tạo booking của Admin/Recommendation đều kiểm tra giữ chỗ đổi lịch.
- Báo giá, phiên bản chính sách, giá sảnh, phí và số tiền đã thanh toán được snapshot.
- Worker chạy mỗi phút để đối soát PayOS trước khi giải phóng giữ chỗ hết hạn; tiền đến muộn chuyển `RECONCILIATION_REQUIRED`.
