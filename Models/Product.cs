using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public sealed class Product
{
    public int ProductId { get; set; }
    public int CategoryId { get; set; }
    public ProductCategoryRecord? ProductCategory { get; set; }

    [Key, StringLength(80)]
    public string Slug { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Category { get; set; } = "women";

    [Range(0.01, 100000000)]
    public decimal Price { get; set; }

    [StringLength(100)]
    public string ImageId { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(100)]
    public string SizesCsv { get; set; } = "S,M,L";

    [StringLength(60)]
    public string Color { get; set; } = string.Empty;

    [Range(0, 100000)]
    public int Stock { get; set; } = 20;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string[] Sizes => SizesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
