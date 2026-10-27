using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.Banners;

public sealed class BannerViewModel
{
    public int BannerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string NavigationLabel { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageAlt { get; set; } = string.Empty;
    public string PrimaryActionType { get; set; } = "NONE";
    public string? PrimaryButtonLabel { get; set; }
    public string? PrimaryLink { get; set; }
    public string SecondaryActionType { get; set; } = "NONE";
    public string? SecondaryButtonLabel { get; set; }
    public string? SecondaryLink { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Version { get; set; } = string.Empty;
}

public sealed class BannerFormViewModel
{
    public int BannerId { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Subtitle { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Description { get; set; } = string.Empty;
    [Required, StringLength(100)] public string NavigationLabel { get; set; } = string.Empty;
    [Required, StringLength(250)] public string ImageAlt { get; set; } = string.Empty;
    [Required] public string PrimaryActionType { get; set; } = "NONE";
    public string? PrimaryButtonLabel { get; set; }
    public string? PrimaryLink { get; set; }
    [Required] public string SecondaryActionType { get; set; } = "NONE";
    public string? SecondaryButtonLabel { get; set; }
    public string? SecondaryLink { get; set; }
    [Range(1, int.MaxValue)] public int SortOrder { get; set; } = 1;
    public bool IsVisible { get; set; }
    public string? Version { get; set; }
    public IFormFile? Image { get; set; }
}

public sealed class BannerManagementViewModel
{
    public IReadOnlyList<BannerViewModel> Banners { get; init; } = [];
}
