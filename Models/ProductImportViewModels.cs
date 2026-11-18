using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public sealed class ProductBulkImportViewModel
{
    [Required, StringLength(250000)]
    public string Rows { get; set; } = string.Empty;
}
