using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public sealed class ProductReview
{
    public int Id { get; set; }
    public int CustomerOrderId { get; set; }
    public CustomerOrder Order { get; set; } = null!;

    [Required, StringLength(80)]
    public string ProductSlug { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    [StringLength(1000)]
    public string? StaffReply { get; set; }

    [StringLength(100)]
    public string? StaffReplyBy { get; set; }

    public DateTime? StaffRepliedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
