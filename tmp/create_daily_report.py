from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor


ROOT = Path(r"C:\Users\KIEULOAN\Documents\Year 4\KLTN\RevirsidePlace")
OUTPUT = ROOT / "output" / "documents" / "LeMinhLong_BaoCaoNgay_09-10-2026.docx"


def set_run_font(run, *, size=13, bold=False):
    run.font.name = "Times New Roman"
    run._element.get_or_add_rPr().rFonts.set(qn("w:ascii"), "Times New Roman")
    run._element.get_or_add_rPr().rFonts.set(qn("w:hAnsi"), "Times New Roman")
    run._element.get_or_add_rPr().rFonts.set(qn("w:eastAsia"), "Times New Roman")
    run.font.size = Pt(size)
    run.bold = bold


def set_paragraph_layout(paragraph, *, first_line=True, before=0, after=5, line=1.5):
    fmt = paragraph.paragraph_format
    fmt.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    fmt.line_spacing = line
    fmt.space_before = Pt(before)
    fmt.space_after = Pt(after)
    if first_line:
        fmt.first_line_indent = Cm(0.8)


def add_label_paragraph(doc, label, text):
    p = doc.add_paragraph()
    set_paragraph_layout(p, first_line=False, after=5)
    set_run_font(p.add_run(label), bold=True)
    set_run_font(p.add_run(text))
    return p


def add_heading(doc, number, title):
    p = doc.add_paragraph()
    set_paragraph_layout(p, first_line=True, before=5, after=5)
    set_run_font(p.add_run(f"{number}. {title}"), bold=True)
    return p


def add_body(doc, text):
    p = doc.add_paragraph()
    set_paragraph_layout(p, first_line=True, after=5)
    set_run_font(p.add_run(text))
    return p


def build():
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    doc = Document()
    section = doc.sections[0]
    section.page_width = Cm(21.0)
    section.page_height = Cm(29.7)
    section.top_margin = Cm(2.0)
    section.bottom_margin = Cm(2.0)
    section.left_margin = Cm(2.5)
    section.right_margin = Cm(2.5)

    normal = doc.styles["Normal"]
    normal.font.name = "Times New Roman"
    normal._element.rPr.rFonts.set(qn("w:ascii"), "Times New Roman")
    normal._element.rPr.rFonts.set(qn("w:hAnsi"), "Times New Roman")
    normal._element.rPr.rFonts.set(qn("w:eastAsia"), "Times New Roman")
    normal.font.size = Pt(13)

    title_style = doc.styles["Title"]
    title_style.font.name = "Times New Roman"
    title_style.font.color.rgb = RGBColor(0, 0, 0)
    title_style._element.rPr.rFonts.set(qn("w:ascii"), "Times New Roman")
    title_style._element.rPr.rFonts.set(qn("w:hAnsi"), "Times New Roman")
    title_style._element.rPr.rFonts.set(qn("w:eastAsia"), "Times New Roman")
    title_style_ppr = title_style._element.find(qn("w:pPr"))
    if title_style_ppr is not None:
        title_style_border = title_style_ppr.find(qn("w:pBdr"))
        if title_style_border is not None:
            for border in list(title_style_border):
                border.set(qn("w:val"), "nil")

    title = doc.add_paragraph()
    title.style = doc.styles["Title"]
    title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    title.paragraph_format.space_before = Pt(0)
    title.paragraph_format.space_after = Pt(10)
    title.paragraph_format.line_spacing = 1.0
    title_run = title.add_run("BÁO CÁO NGÀY 09/10/2026")
    set_run_font(title_run, size=16, bold=True)
    title_run.font.color.rgb = RGBColor(0, 0, 0)
    title_run.font.hidden = False
    p_pr = title._element.get_or_add_pPr()
    p_bdr = p_pr.find(qn("w:pBdr"))
    if p_bdr is not None:
        p_pr.remove(p_bdr)

    add_label_paragraph(
        doc,
        "Hôm trước: ",
        "Xây dựng chức năng quản lý nhân sự dành cho Quản lý, xử lý yêu cầu thay đổi vai trò và đồng bộ thông báo cho Quản trị viên.",
    )
    add_label_paragraph(
        doc,
        "Hôm nay: ",
        "Hoàn thiện giao diện quản lý nhân sự, bổ nhiệm sảnh và luồng quên mật khẩu, đặt lại mật khẩu, đăng nhập sau khi đổi mật khẩu.",
    )
    result = doc.add_paragraph()
    set_paragraph_layout(result, first_line=False, before=2, after=6)
    set_run_font(result.add_run("Kết quả:"), bold=True)

    add_heading(doc, 1, "Hoàn thiện module quản lý nhân sự dành cho Quản lý")
    add_body(
        doc,
        "Đã xây dựng đầy đủ luồng quản lý nhân sự cho tài khoản Quản lý, gồm xem danh sách nhân viên, tìm kiếm, lọc theo vai trò và trạng thái, xem thông tin chi tiết, theo dõi sảnh đang phụ trách và thực hiện các thao tác nghiệp vụ phù hợp với quyền hạn.",
    )
    add_body(
        doc,
        "Giao diện danh sách và bảng dữ liệu được chuẩn hóa theo phong cách của hệ thống Riverside Palace. Drawer chi tiết nhân viên được bổ sung ảnh đại diện, mã nhân viên, vai trò, trạng thái, thông tin liên hệ, danh sách sảnh phụ trách và nhóm hành động cố định để người dùng dễ theo dõi và thao tác.",
    )

    add_heading(doc, 2, "Hoàn thiện chức năng bổ nhiệm và quản lý sảnh")
    add_body(
        doc,
        "Chức năng bổ nhiệm sảnh được hoàn thiện với danh sách sảnh có trạng thái rõ ràng, phân biệt sảnh sẵn sàng và sảnh cần thay thế người phụ trách. Hệ thống hiển thị số lượng sảnh đã chọn, cảnh báo khi việc bổ nhiệm có thể thay thế Quản lý sảnh hiện tại và chỉ cho phép xác nhận khi dữ liệu hợp lệ.",
    )
    add_body(
        doc,
        "Các nút hành động nền vàng trong cùng nhóm chức năng được đồng bộ chữ màu đen; nút nguy hiểm và nút trạng thái vẫn giữ màu riêng để không làm mất ý nghĩa thao tác. Lỗi lớp phủ modal luôn hiển thị và chặn giao diện cũng đã được xử lý, bảo đảm modal chỉ mở khi người dùng thực hiện hành động tương ứng.",
    )

    add_heading(doc, 3, "Hoàn thiện yêu cầu thay đổi vai trò và thông báo")
    add_body(
        doc,
        "Đã hoàn thiện luồng Quản lý gửi yêu cầu thay đổi vai trò giữa Nhân viên điều phối và Quản lý sảnh. Yêu cầu được kiểm tra theo trạng thái hiện tại, ngăn tạo trùng khi đã có yêu cầu đang chờ xử lý và lưu thông tin để Quản trị viên xem xét, phê duyệt hoặc từ chối.",
    )
    add_body(
        doc,
        "Luồng thông báo cho Quản trị viên được kiểm tra từ bước tạo yêu cầu đến dữ liệu chuông thông báo. Giao diện xử lý yêu cầu được điều chỉnh lại modal xác nhận, nội dung từ chối, nút kết thúc và trạng thái hiển thị nhằm giúp thao tác rõ ràng, nhất quán hơn.",
    )

    add_heading(doc, 4, "Hoàn thiện luồng cấp và đổi mật khẩu ban đầu")
    add_body(
        doc,
        "Đã bổ sung quy trình cấp mật khẩu ban đầu cho tài khoản nhân viên và nội dung thông báo tương ứng. Hệ thống hỗ trợ gửi mã xác thực qua email, kiểm tra thời hạn và trạng thái sử dụng của mã, đồng thời yêu cầu người dùng đổi mật khẩu khi đăng nhập lần đầu để bảo đảm an toàn tài khoản.",
    )
    add_body(
        doc,
        "Các thành phần xử lý email, mã OTP, dữ liệu yêu cầu đổi mật khẩu và cấu hình dịch vụ được tách riêng ở Backend. Phần Frontend hiển thị thông báo ngắn gọn, đúng ngữ cảnh và giữ thống nhất với giao diện đăng nhập, đổi mật khẩu của hệ thống.",
    )

    add_heading(doc, 5, "Sửa lỗi quên mật khẩu và đăng nhập sau khi đặt lại")
    add_body(
        doc,
        "Đã kiểm tra toàn bộ luồng quên mật khẩu từ gửi email, mở liên kết đặt lại, cập nhật mật khẩu đến đăng nhập bằng mật khẩu mới. Lỗi HTTP 400 do phiên đăng nhập bị xóa trước khi gửi biểu mẫu đã được khắc phục bằng cách chỉ xóa phiên sau khi API xác nhận cập nhật mật khẩu thành công.",
    )
    add_body(
        doc,
        "Đồng thời, hệ thống loại bỏ các địa chỉ chuyển tiếp không hợp lệ như trang đăng xuất, đăng nhập, quên mật khẩu và đặt lại mật khẩu. Trường hợp truy cập đường dẫn đăng xuất bằng phương thức GET từ lịch sử trình duyệt được chuyển về trang phù hợp thay vì trả về HTTP 405. Toàn bộ solution đã được build kiểm tra thành công, không phát sinh lỗi hoặc cảnh báo.",
    )

    # Keep title metadata clean and make the file easy to identify in Word.
    doc.core_properties.title = "Báo cáo ngày 09/10/2026"
    doc.core_properties.subject = "Báo cáo tiến độ hệ thống Riverside Palace"
    doc.core_properties.author = "Lê Minh Long"
    doc.save(OUTPUT)
    print(OUTPUT)


if __name__ == "__main__":
    build()
