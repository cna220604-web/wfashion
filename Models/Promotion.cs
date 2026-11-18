using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public static class PromotionTypes
{
    public const string Percentage = "Percentage";
    public const string FixedAmount = "FixedAmount";
    public static readonly string[] All = [Percentage, FixedAmount];
}

public sealed class Promotion
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string NormalizedCode { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Type { get; set; } = PromotionTypes.Percentage;

    public decimal Value { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public int? MaximumRedemptions { get; set; }
    public int RedemptionCount { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public decimal CalculateDiscount(decimal subtotal) => Type == PromotionTypes.Percentage
        ? Math.Min(subtotal * Value / 100m, MaximumDiscountAmount ?? decimal.MaxValue)
        : Math.Min(subtotal, Value);

    public bool IsAvailable(DateTime nowUtc) => IsActive
        && StartsAtUtc <= nowUtc
        && (EndsAtUtc is null || EndsAtUtc > nowUtc)
        && (MaximumRedemptions is null || RedemptionCount < MaximumRedemptions);
}

public sealed class PromotionCreateViewModel
{
    [Required, StringLength(30, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Mã chỉ gồm chữ cái, chữ số, dấu gạch ngang hoặc gạch dưới.")]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = PromotionTypes.Percentage;

    [Range(typeof(decimal), "1", "1000000000")]
    public decimal Value { get; set; } = 10;

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal MinimumOrderAmount { get; set; }

    [Range(typeof(decimal), "1", "1000000000")]
    public decimal? MaximumDiscountAmount { get; set; }

    [Range(1, 1000000)]
    public int? MaximumRedemptions { get; set; }

    public DateOnly StartsOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    public DateOnly? EndsOn { get; set; }
}

public sealed class PromotionPreviewRequest
{
    [Required, StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(30)]
    public List<CheckoutLineRequest> Items { get; set; } = [];
}
