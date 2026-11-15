using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public sealed class ProductCategoryRecord
{
    public int CategoryId { get; set; }

    [Required, StringLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
