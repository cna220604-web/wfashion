using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public sealed class BusinessPartner
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string NormalizedCode { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string NormalizedName { get; set; } = string.Empty;

    [Required, StringLength(240)]
    public string Address { get; set; } = string.Empty;

    [StringLength(100)]
    public string ContactName { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class StockReceiptNumber
{
    public int Id { get; set; }
}

public sealed class BusinessPartnerCreateViewModel
{
    public int Id { get; set; }

    [Required, StringLength(30, MinimumLength = 2), RegularExpression("^[A-Za-z0-9-]{2,30}$", ErrorMessage = "Mã đối tác chỉ gồm chữ cái, số và dấu gạch ngang.")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(240, MinimumLength = 5)]
    public string Address { get; set; } = string.Empty;

    [StringLength(100)]
    public string ContactName { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}
