using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public sealed class ProductBulkImportViewModel
{
    [Required, StringLength(250000)]
    public string Rows { get; set; } = string.Empty;
}

public sealed class QuickProductCreateViewModel
{
    [Required, StringLength(20, MinimumLength = 2), RegularExpression("^[A-Za-z0-9-]{2,20}$", ErrorMessage = "Mã chỉ gồm chữ cái, số và dấu gạch ngang; tối đa 20 ký tự.")]
    public string Slug { get; set; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, RegularExpression("^(women|men|accessories)$")]
    public string Category { get; set; } = "women";

    [Range(1000, 100000000)]
    public decimal Price { get; set; }

    [StringLength(60)]
    public string Color { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string SizesCsv { get; set; } = "S,M,L";
}
