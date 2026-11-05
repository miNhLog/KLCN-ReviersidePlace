---
name: admin-ui
description: "Thiết kế, chỉnh sửa, tái cấu trúc có kiểm soát hoặc đánh giá giao diện admin/dashboard theo yêu cầu người dùng; áp dụng Metis Bootstrap Admin làm nguồn tham chiếu khi có, đặc biệt cho Riverside Palace ASP.NET Core MVC Razor. Kích hoạt khi người dùng yêu cầu admin UI, sidebar, dashboard, quản lý, bảng dữ liệu, biểu đồ, modal, responsive, hoặc yêu cầu viết prompt Codex cho các việc này. Không tự ý sửa code khi người dùng chỉ muốn thảo luận hay soạn prompt."
---

# Admin UI: chỉnh giao diện theo ý người dùng

## Mục tiêu

Biến yêu cầu thiết kế của người dùng thành thay đổi giao diện thực sự phù hợp với dự án hiện tại. Tham khảo Metis Admin Dashboard ở mức bố cục và thành phần UI khi có mẫu, nhưng luôn ưu tiên yêu cầu mới nhất của người dùng, ảnh tham chiếu, hệ thống thiết kế hiện tại và giới hạn phạm vi công việc.

**Nguyên tắc bắt buộc:** chỉnh đúng nơi được yêu cầu, giữ nguyên nghiệp vụ và dữ liệu. Không tự thay thế toàn bộ dashboard, không chuyển framework, không đổi luồng API hoặc phân quyền chỉ để đạt một kiểu giao diện.

## 1. Xác định chế độ làm việc trước tiên

Đọc yêu cầu hiện tại và các ràng buộc người dùng đã chốt. Chọn một chế độ:

- **Thảo luận/đề xuất:** phân tích điểm cần sửa, phương án bố cục, tính khả thi và các đánh đổi. Không chỉnh file.
- **Viết prompt cho Codex:** xuất prompt triển khai có phạm vi, tiêu chí hoàn thành và phương án kiểm thử; không chỉnh file hoặc Git.
- **Chỉnh sửa trực tiếp:** thực hiện trên source hiện tại chỉ khi người dùng yêu cầu sửa. Giới hạn thay đổi ở các trang/thành phần được chọn.
- **Rà soát UI:** đánh giá giao diện theo ảnh, code hoặc yêu cầu; phân biệt lỗi quan sát được với đề xuất. Không chỉnh file nếu chưa được yêu cầu.

Nếu người dùng đã cung cấp đủ dữ liệu, tự chọn mặc định thiết kế hợp lý và thực hiện trong phạm vi được phép. Chỉ hỏi khi thiếu một quyết định có ảnh hưởng đáng kể đến kết quả và không thể suy ra.

## 2. Thu thập nguồn tham chiếu và ưu tiên

Thứ tự ưu tiên khi có xung đột:

1. Yêu cầu cụ thể trong tin nhắn mới nhất, bao gồm trang cần sửa, vị trí, màu sắc, hành vi và điều cấm thay đổi.
2. Ảnh chụp giao diện hiện tại, ảnh mong muốn, bản phác thảo, file hoặc component người dùng gửi.
3. Design system, CSS tokens, layouts, assets, components đang dùng trong dự án.
4. Metis Admin Dashboard hoặc mẫu giao diện khác do người dùng chỉ định.
5. Quy ước UI/UX thông dụng cho admin dashboard.

Nếu được cung cấp `metis-v1.0.0.zip`, đọc nội dung thật trước khi tham khảo. Tệp ZIP không được giả định là đang hiện diện trong mọi phiên Codex. Kiểm tra file đầu vào và vị trí hợp lệ, không tự nhận đã đọc khi chưa truy cập được.

**Các điểm tham khảo thực tế trong Metis:**

- `src-modern/index.html`, `analytics.html`, `reports.html`: trang dashboard, KPI, báo cáo và biểu đồ.
- `src-modern/elements-tables.html`, `elements-forms.html`, `elements-modals.html`, `calendar.html`: bảng, biểu mẫu, modal và lịch.
- `src-modern/styles/scss/abstracts/_variables.scss`: biến màu, khoảng cách, radius, shadows.
- `src-modern/styles/scss/layout/` và `src-modern/styles/scss/components/`: sidebar, header, cards, tables, forms và charts.
- `src-modern/scripts/components/sidebar.js`: xử lý sidebar desktop/mobile.
- `src-modern/scripts/utils/chart-palette.js`: bảng màu biểu đồ, tách màu thể hiện trạng thái với màu dữ liệu.
- `docs/customization.md`, `docs/components.md`, `DEVELOPMENT.md`: hướng dẫn giao diện và kỹ thuật.

Metis sử dụng Bootstrap 5, Alpine.js, ApexCharts, SCSS và Vite. Đây là **mẫu tham khảo**, không phải lý do để chuyển một ứng dụng ASP.NET Core MVC Razor sang Vite hay buộc cài toàn bộ dependency của Metis. Chỉ mang qua thành phần và tài nguyên thật sự cần, sau khi kiểm tra tương thích.

**Bản quyền:** Metis trong tài liệu tham chiếu có giấy phép MIT, thông báo bản quyền Colorlib và những người đóng góp. Khi sao chép phần mã hoặc tài nguyên đáng kể từ Metis, giữ nguyên thông báo bản quyền và giấy phép tương ứng trong dự án. Không tự ý sao chép thương hiệu/logo của Metis vào sản phẩm Riverside Palace. Kiểm tra giấy phép riêng của ảnh, icon và font bên thứ ba trước khi sử dụng.

## 3. Bối cảnh Riverside Palace (áp dụng khi làm dự án này)

Dự án tham chiếu: `miNhLog/KLCN-ReviersidePlace`.

Kiến trúc thường gặp:

- Frontend: `Frontend/HeThongDatTiecCuoi_WEB`, ASP.NET Core MVC, Razor Views, CSS và JavaScript.
- Backend: `Backend/HeThongDatTiecCuoi_API`, ASP.NET Core Web API, DTO, service và kiểm tra quyền.
- Database: SQL Server và Entity Framework Core.
- AI: `AI_Service`, Python/FastAPI.

Các vai trò liên quan có thể gồm Quản trị viên, Quản lý, Quản lý sảnh, Điều phối viên và Khách hàng. Kiểm tra tên role và quyền thực tế trong source, không giả định role nào có quyền gì chỉ từ tên gọi.

**Mặc định thiết kế khi người dùng chưa chỉ rõ:** duy trì nhận diện Riverside Palace hiện hữu, ưu tiên nền sáng, xanh navy, kem/trắng và vàng champagne theo CSS hiện tại. Metis chỉ định hướng lưới, nhịp khoảng trắng, sự rõ ràng của KPI, bảng, bộ lọc và biểu đồ. Không thay logo, menu, nhãn trạng thái, màu thương hiệu hoặc layout dùng chung khi yêu cầu chỉ nhắm vào một trang con.

Ví dụ đối tượng thường chỉnh: Dashboard, lịch sảnh, danh sách Booking, thống kê đánh giá tiệc cưới, thanh toán/hợp đồng, QR đánh giá, quản lý nhân sự, điều phối và checklist. Mỗi lần chỉ thay những phần được yêu cầu.

## 4. Quy trình chỉnh sửa trên source

### Bước A. Tìm đúng nơi chịu trách nhiệm

1. Kiểm tra trạng thái repository và thay đổi chưa commit; không ghi đè thay đổi đang có của người dùng.
2. Xác định URL/route của trang và các Razor View, partial, Layout, controller, view model, CSS, JS liên quan.
3. Tìm stylesheet, token, component đã tồn tại trước khi viết mới.
4. Theo dõi đường đi dữ liệu từ API/model đến UI, xác định các selector và event handler hiện đang phục vụ nghiệp vụ.
5. Với thành phần dùng chung như sidebar, header hoặc modal, kiểm tra các trang bị ảnh hưởng trước khi sửa.

Nếu chỉ có ảnh mà không có code, mô tả những gì quan sát được; không khẳng định về cấu trúc implementation.

### Bước B. Thiết kế thay đổi tối thiểu

Trước khi sửa, xác định:

- **Giữ nguyên:** chức năng, route, quyền truy cập, API contract, trường biểu mẫu, tên action, trạng thái, số liệu và các event đang hoạt động.
- **Được thay:** HTML/CSS của khu vực được chọn, bố cục, khoảng trắng, font đang có quyền sử dụng, icon, màu, responsive, hiệu ứng và biểu đồ khi được yêu cầu.
- **Không được tự ý thay:** model/schema DB, controller/service nghiệp vụ, xác thực, thanh toán, thời gian QR, dữ liệu thật, cấu hình bí mật, phần khác không nằm trong phạm vi yêu cầu.

Chỉ thay đổi Backend khi việc hiển thị chức năng được yêu cầu thực sự thiếu API/dữ liệu. Khi đó nêu rõ lý do và bảo toàn hợp đồng API có sẵn, nhất là các API được dùng ở trang khác.

### Bước C. Hiện thực giao diện

- Ưu tiên dùng Layout, Razor partial và components sẵn có; tránh nhân bản sidebar/header trên từng View.
- Dùng CSS có phạm vi theo trang hoặc component; tránh selector toàn cục gây hỏng trang khác. Sửa design tokens dùng chung chỉ khi người dùng muốn đồng bộ toàn hệ thống.
- Tận dụng thư viện hiện có. Chỉ bổ sung Bootstrap/Alpine/ApexCharts hoặc thư viện khác khi thật sự cần và tương thích, không nạp trùng Bootstrap hoặc nhiều bộ icon.
- Nếu dùng Ajax/fetch, giữ nguyên xác thực/cookie, anti-forgery token, input validation, error handling, timeout và response contract đang áp dụng.
- Nếu dữ liệu từ API chưa có, hiển thị empty state hợp lý. Không dùng dữ liệu giả trên giao diện vận hành rồi trình bày như dữ liệu thật.
- Với bảng và biểu đồ, nhất quán cách tính chỉ số, màu trạng thái, bộ lọc và quyền xem dữ liệu.
- Không dùng `innerHTML` cho nội dung do người dùng nhập nếu chưa bảo đảm nội dung được làm sạch; ưu tiên binding/encoding an toàn.
- Bảo đảm tương tác keyboard, focus visible, label của form, aria khi cần, và tương phản chữ đủ đọc.

### Bước D. Kiểm tra thực tế

1. Chạy build/test phù hợp với dự án nếu môi trường hỗ trợ; ghi đúng câu lệnh và kết quả, không tuyên bố pass nếu chưa chạy.
2. Kiểm tra thao tác quan trọng: điều hướng, search, filter, pagination, modal, submit, validation, chart, sidebar, dropdown và nút hành động bị ảnh hưởng.
3. Kiểm tra responsive ở kích thước tham chiếu 1440×900, 768×1024 và 390×844 nếu có khả năng xem giao diện. Không được có thanh cuộn ngang không chủ đích, bảng hoặc dropdown bị cắt.
4. Kiểm tra trường hợp loading, empty/error, chuỗi dài, dữ liệu lớn và các vai trò khác nhau nếu có.
5. Kiểm tra lại các trang cùng dùng Layout/CSS/JS đã thay đổi. So sánh với ảnh tham chiếu khi có.
6. Báo cáo rõ điểm còn thiếu khả năng kiểm chứng nếu không chạy được browser, DB hoặc project.

Không `git push`, deploy, merge, tạo PR hay thay đổi nhánh từ xa trừ khi được yêu cầu rõ ràng. Không xóa/ghi đè thay đổi chưa commit của người dùng.

## 5. Nguyên tắc thiết kế admin dashboard

### Bố cục

- Giữ phân cấp rõ ràng: tiêu đề trang → KPI/tổng quan → bộ lọc → nội dung chính → chi tiết.
- Dùng grid, spacing và chiều rộng nhất quán; header, sidebar và content không chồng lên nhau.
- Bảng nhiều cột phải xử lý responsive bằng scroll trong bảng, ẩn cột phụ có kiểm soát hoặc thiết kế thẻ trên mobile.
- Nút chính/phụ và badge trạng thái phải có phân cấp thị giác, tránh mọi thành phần đều nổi bật ngang nhau.

### KPI và thống kê

- Chỉ số phải lấy từ API/dữ liệu thật, có định nghĩa và đơn vị rõ ràng.
- Không nhầm số Booking với số lượt đánh giá hoặc số lượt thanh toán.
- Nếu thiếu dữ liệu cho điểm trung bình hay phần trăm, hiển thị "Chưa có dữ liệu", không tự gán `0/5` hoặc `0%`.
- Nếu có thống kê tổng quan và thống kê theo từng Booking, giữ rõ phạm vi: bộ lọc tổng quan chỉ tác động vào tổng quan theo yêu cầu, khu vực chi tiết phải truy vấn theo Booking đã chọn.
- Với đánh giá sao, chỉ phân loại 4–5 sao là tích cực, 3 sao trung lập, 1–2 sao tiêu cực nếu người dùng chấp nhận ngưỡng này; không tự gắn nhãn "AI sentiment" cho phân loại theo điểm số.
- Chart phải có nhãn trục/legend/tooltip rõ, số liệu khớp KPI và có giải pháp khi không thể render chart.

### Form, bảng và modal

- Giữ trường dữ liệu, `name`, `id`, route submit, validation, anti-forgery và JS hooks đang được nghiệp vụ sử dụng; nếu phải đổi selector, sửa toàn bộ nơi tham chiếu và test lại.
- Modal đóng/mở đúng, khóa focus nếu hợp lý, có hành động hủy, xử lý click ngoài và Escape theo chuẩn đang dùng.
- Form phải phân biệt trạng thái disabled, readonly, loading, success và error; chống gửi lặp khi cần.
- Không ẩn các chức năng cần thiết chỉ để giao diện gọn hơn; nếu gom trong menu, bảo đảm vẫn dễ tìm.

### Sidebar và header

- Xác định cơ chế toggle đang dùng, tránh gắn nhiều listener trùng cho cùng nút.
- Desktop có thể thu gọn sidebar; mobile dùng overlay/backdrop nếu phù hợp với giao diện gốc.
- Khi được yêu cầu đổi vị trí logo, áp dụng căn chỉnh theo tọa độ viewport nếu người dùng yêu cầu chính xác, không chỉ căn giữa không gian trống giữa menu và nút.
- Không đổi nhãn, route và visibility theo role nếu người dùng không yêu cầu.

## 6. Mẫu cách hiểu yêu cầu người dùng

**Yêu cầu:** "Dùng Metis để làm lại trang thống kê đánh giá nhưng giữ sidebar của Riverside Palace."

**Phạm vi:** chỉ trang đánh giá và CSS/JS của trang; đọc Metis analytics/reports/cards/charts làm tham chiếu; giữ Layout/sidebar hiện hành; KPI và biểu đồ lấy dữ liệu thật; không thay API hoặc quyền; kiểm tra responsive và empty state.

**Yêu cầu:** "Chỉnh sidebar toàn bộ admin giống Metis, giữ màu Riverside."

**Phạm vi:** partial/Layout sidebar và stylesheet/script chung liên quan; kiểm tra các nhóm vai trò, active menu, desktop collapse, mobile overlay, các route và các trang chia sẻ layout. Không mang logo của Metis vào trang.

**Yêu cầu:** "Viết prompt để Codex chỉnh UI."

**Phạm vi:** chỉ viết prompt rõ file cần khảo sát, phần cần thay, phần phải giữ, tiêu chí chấp nhận và kiểm thử. Không sửa source.

## 7. Cấu trúc đầu ra theo chế độ

**Nếu chỉ cần ý tưởng hoặc đánh giá:** nêu vấn đề thật, phương án thiết kế và mức ưu tiên; không tự nhận đã chỉnh mã nguồn.

**Nếu cần prompt Codex:** cung cấp prompt có mục tiêu, phạm vi trang/role, tài nguyên tham chiếu, ràng buộc không đụng nghiệp vụ, yêu cầu UI/UX, hành vi cụ thể, dữ liệu/API liên quan, kiểm thử và đầu ra cần báo cáo. Tránh những lệnh mơ hồ như "làm hiện đại hơn" mà không nêu tiêu chí nhận biết.

**Nếu đã chỉnh trực tiếp:** liệt kê file đã sửa, những thay đổi thực tế, kết quả build/test, phần chưa xác minh và cách xem màn hình. Không ghi "đã hoàn tất" nếu chưa kiểm tra các tác vụ quan trọng.

## 8. Điều kiện dừng và bảo toàn dự án

Dừng trước thao tác có nguy cơ làm mất dữ liệu, thay đổi bí mật, thay schema hoặc thay đổi nghiệp vụ ngoài phạm vi được giao. Không tự suy diễn một yêu cầu làm đẹp UI thành quyền tái cấu trúc toàn bộ hệ thống.

Khi nhiệm vụ bị giới hạn về thời gian/công cụ, ưu tiên hoàn thành phần có thể xác minh được, nêu rõ phần chưa hoàn thành; không bịa screenshot, kết quả build hay trạng thái Git.

