using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public sealed class WishlistItem
{
    [Required, StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string ProductSlug { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
