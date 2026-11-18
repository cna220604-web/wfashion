using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models;

public sealed class StockReceipt
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    public string ProductSlug { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string ProductName { get; set; } = string.Empty;

    [Range(1, 100000)]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "1", "9999999999.99")]
    public decimal UnitCost { get; set; }

    [StringLength(160)]
    public string Supplier { get; set; } = string.Empty;

    [StringLength(80)]
    public string Reference { get; set; } = string.Empty;

    public DateOnly ReceivedOn { get; set; }

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;

    [Required, StringLength(450)]
    public string RecordedBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public decimal TotalCost => Quantity * UnitCost;
}

public sealed class OperatingExpense
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Category { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "9999999999.99")]
    public decimal Amount { get; set; }

    [StringLength(120)]
    public string Supplier { get; set; } = string.Empty;

    public DateOnly SpentOn { get; set; }

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;

    [Required, StringLength(450)]
    public string RecordedBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class StockReceiptBatchViewModel
{
    [Required, MinLength(1), MaxLength(200)]
    public List<StockReceiptLineViewModel> Items { get; set; } = [];

    [Range(1, int.MaxValue)]
    public int PartnerId { get; set; }

    public DateOnly ReceivedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class StockReceiptLineViewModel
{
    [Required, StringLength(20)]
    public string ProductSlug { get; set; } = string.Empty;

    [Range(1, 100000)]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "1", "9999999999.99")]
    public decimal UnitCost { get; set; }
}

public sealed class OperatingExpenseViewModel
{
    [Required, StringLength(80)]
    public string Category { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "9999999999.99")]
    public decimal Amount { get; set; }

    [StringLength(120)]
    public string Supplier { get; set; } = string.Empty;

    public DateOnly SpentOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;
}
