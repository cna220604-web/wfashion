using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Pending, Processing, Shipped, Delivered, Cancelled];
}

public sealed class CustomerOrder
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(240)]
    public string Address { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Note { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = OrderStatuses.Pending;

    [Required, StringLength(20)]
    public string PaymentMethod { get; set; } = "COD";

    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAtUtc { get; set; }
    public DateTime? ReturnRequestedAtUtc { get; set; }

    [StringLength(1000)]
    public string? ReturnReason { get; set; }

    [StringLength(50)]
    public string? ReturnVideoId { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}

public sealed class OrderItem
{
    public int Id { get; set; }
    public int CustomerOrderId { get; set; }
    public CustomerOrder Order { get; set; } = null!;

    [Required, StringLength(120)]
    public string ProductName { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string ProductSlug { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Size { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
