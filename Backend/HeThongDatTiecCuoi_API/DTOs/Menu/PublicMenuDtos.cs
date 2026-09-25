namespace HeThongDatTiecCuoi_API.DTOs.Menu;

public sealed class PublicMenuDto
{
    public int MenuId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerTable { get; set; }
    public int DishCount { get; set; }
}

public sealed class PublicMenuListResponse
{
    public IReadOnlyList<PublicMenuDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
