using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Recommendation;

// 1. DTO Yêu cầu khảo sát thông minh toàn diện
public class RecommendationRequestDto
{
    // --- Nhóm 1: Thông tin liên hệ & Cặp đôi ---
    [Required(ErrorMessage = "Vui lòng nhập họ tên người đại diện / cặp đôi")]
    [MaxLength(150)]
    public string CoupleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại liên hệ")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string? Email { get; set; }

    // --- Nhóm 2: Thời gian & Độ linh hoạt ---
    [Required(ErrorMessage = "Vui lòng chọn ngày dự kiến")]
    public DateTime EventDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ca tổ chức")]
    public string Shift { get; set; } = "Ca tối"; // "Ca trưa" hoặc "Ca tối"

    public bool IsDateFlexible { get; set; } = false; // Có thể linh hoạt đổi ngày +- 1 tuần nếu sảnh đẹp hết chỗ

    // --- Nhóm 3: Quy mô & Ngân sách ---
    [Required(ErrorMessage = "Vui lòng nhập số lượng bàn chính thức")]
    [Range(5, 250, ErrorMessage = "Số lượng bàn từ 5 đến 250 bàn")]
    public int OfficialTableCount { get; set; }

    [Range(0, 30, ErrorMessage = "Số bàn dự phòng từ 0 đến 30 bàn")]
    public int SpareTableCount { get; set; } = 2; // Bàn dự phòng (sơ cua)

    public int TotalTables => OfficialTableCount + SpareTableCount;

    [Range(50, 3000, ErrorMessage = "Số lượng khách từ 50 đến 3000")]
    public int GuestCount { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập ngân sách dự kiến")]
    [Range(50000000, 3000000000, ErrorMessage = "Ngân sách từ 50 triệu đến 3 tỷ VNĐ")]
    public decimal ExpectedBudget { get; set; }

    // Ưu tiên tỷ trọng chi phí: "BALANCED" (Cân đối), "PRIORITIZE_FOOD" (Đầu tư món ăn), "PRIORITIZE_DECOR" (Đầu tư không gian)
    public string BudgetPriority { get; set; } = "BALANCED";

    // --- Nhóm 4: Phong cách & Không gian sảnh ---
    // "INDOOR_PILLARLESS" (Đại sảnh trần cao không cột), "OUTDOOR_RIVER" (Sân vườn ven sông), "COZY_INTIMATE" (Ấm cúng)
    public string? PreferredSpaceType { get; set; }

    // "Hoàng gia", "Lãng mạn", "Hiện đại", "Cổ điển Á Đông", "Sân vườn / Rustic"
    public string? DesiredStyle { get; set; }

    // "Vàng Gold hoàng tộc", "Trắng tinh khôi", "Xanh Navy huyền ảo", "Đỏ nhung truyền thống", "Hồng pastel"
    public string? MainColorTone { get; set; }

    // --- Nhóm 5: Khẩu vị ẩm thực & Dịch vụ đi kèm ---
    // "ASIAN_TRADITIONAL" (Á Đông tinh hoa), "EUROPEAN_FUSION" (Âu - Á kết hợp), "SEAFOOD_PREMIUM" (Thiên về hải sản cao cấp)
    public string? FoodPreference { get; set; }

    public bool HasVegetarianOption { get; set; } = false; // Có nhu cầu phục vụ thêm bàn chay / món ăn kiêng

    // Danh sách dịch vụ bổ trợ mong muốn ("DV001", "DV002", "DV003"...)
    public List<string> SelectedServiceCodes { get; set; } = new();

    public string? SpecialRequests { get; set; } // Ghi chú thêm
}

// 2. DTO Kết quả chi tiết một Combo được thuật toán lựa chọn
public class ComboSuggestionDto
{
    public string ComboType { get; set; } = string.Empty; // "ECONOMY", "BALANCED", "LUXURY"
    public string ComboTitle { get; set; } = string.Empty; // Tiêu đề định danh combo
    public string ComboBadge { get; set; } = string.Empty;
    public double OverallMatchScore { get; set; } // % Độ tương thích tổng thể (0 - 100%)

    // Điểm thành phần chi tiết (Thuận tiện khi demo bảo vệ thuật toán)
    public double BudgetMatchScore { get; set; }  // Điểm tài chính (Trọng số 35%)
    public double SpaceFitScore { get; set; }     // Điểm độ vừa vặn không gian (Trọng số 25%)
    public double StyleThemeScore { get; set; }   // Điểm phong cách & tone màu (Trọng số 25%)
    public double RatingCredibilityScore { get; set; } // Điểm tín nhiệm lịch sử 5 sao (Trọng số 15%)

    // Sảnh tiệc
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string HallCode { get; set; } = string.Empty;
    public int HallCapacity { get; set; }
    public decimal HallRentalPrice { get; set; }
    public string HallDescription { get; set; } = string.Empty;

    // Thực đơn
    public int MenuId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public decimal MenuPricePerTable { get; set; }
    public decimal MenuTotalPrice { get; set; } // = MenuPricePerTable * TotalTables
    public string MenuDescription { get; set; } = string.Empty;

    // Gói trang trí
    public int DecorationPackageId { get; set; }
    public string DecorationPackageName { get; set; } = string.Empty;
    public string DecorationStyle { get; set; } = string.Empty;
    public decimal DecorationPrice { get; set; }
    public string DecorationDescription { get; set; } = string.Empty;

    // Các dịch vụ cộng thêm phù hợp
    public List<SuggestedServiceItemDto> IncludedServices { get; set; } = new();
    public decimal ServicesTotalPrice { get; set; }

    // Tài chính tổng kết
    public decimal TotalEstimatedCost { get; set; }
    public decimal BudgetDifference { get; set; } // Chênh lệch (Dương: dư ngân sách, Âm: vượt ngân sách)
    public string BudgetAnalysisText { get; set; } = string.Empty;

    // Lý do khuyến nghị (Explainable AI - XAI)
    public List<string> RecommendationReasons { get; set; } = new();
}

// 3. DTO chi tiết dịch vụ đi kèm
public class SuggestedServiceItemDto
{
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Note { get; set; } = string.Empty;
}

// 4. DTO Phản hồi toàn diện
public class RecommendationResponseDto
{
    public string InquiryCode { get; set; } = string.Empty; // Mã yêu cầu khảo sát
    public string CoupleName { get; set; } = string.Empty;
    public int TotalCandidatesEvaluated { get; set; } // Tổng số combo đã phân tích (VD: 504 combo)
    public List<ComboSuggestionDto> TopCombos { get; set; } = new();
    public string AlgorithmNotice { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.Now;
}