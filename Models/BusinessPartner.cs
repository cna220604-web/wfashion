using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public sealed class BusinessPartner
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string NormalizedName { get; set; } = string.Empty;

    [StringLength(100)]
    public string ContactName { get; set; } = string.Empty;

    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class StockReceiptNumber
{
    public int Id { get; set; }
}

public sealed class BusinessPartnerCreateViewModel
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string ContactName { get; set; } = string.Empty;

    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;
}
