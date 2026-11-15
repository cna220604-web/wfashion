using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public sealed class FashionVideo
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string VideoFileName { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string ProductSlug { get; set; } = string.Empty;

    [Required, StringLength(180)]
    public string Caption { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
