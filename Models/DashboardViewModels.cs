namespace WebApplication1.Models;

public sealed class StorefrontViewModel
{
    public IReadOnlyList<Product> Products { get; init; } = [];
    public IReadOnlyList<StorefrontFashionVideo> FashionVideos { get; init; } = [];
    public IReadOnlyDictionary<string, ProductReviewSummary> Reviews { get; init; } = new Dictionary<string, ProductReviewSummary>();
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string CustomerAddress { get; init; } = string.Empty;
}

public sealed record ProductReviewSummary(double AverageRating, int ReviewCount);

public sealed record StorefrontFashionVideo(int Id, string VideoFileName, string ProductSlug, string ProductName, decimal ProductPrice, string Caption);

public sealed class AdminDashboardViewModel
{
    public int CustomerCount { get; init; }
    public int EmployeeCount { get; init; }
    public int ProductCount { get; init; }
    public int PendingOrderCount { get; init; }
    public decimal SalesTotal { get; init; }
    public decimal StockPurchaseTotal { get; init; }
    public decimal OperatingExpenseTotal { get; init; }
    public decimal RecordedCashFlow { get; set; }
    public IReadOnlyList<StockReceipt> RecentStockReceipts { get; init; } = [];
    public IReadOnlyList<OperatingExpense> RecentOperatingExpenses { get; init; } = [];
    public IReadOnlyList<Product> Products { get; init; } = [];
    public IReadOnlyList<FashionVideo> FashionVideos { get; init; } = [];
    public IReadOnlyList<AdminUserRow> Users { get; init; } = [];
    public IReadOnlyList<CustomerOrder> RecentOrders { get; init; } = [];
}

public sealed record AdminUserRow(string Id, string Email, string DisplayName, string Role);

public sealed class StaffDashboardViewModel
{
    public int PendingOrderCount { get; init; }
    public int ProcessingOrderCount { get; init; }
    public int ShippedOrderCount { get; init; }
    public int LowStockCount { get; init; }
    public int ProductReviewCount { get; init; }
    public string? StatusFilter { get; init; }
    public string OrderSearch { get; init; } = string.Empty;
    public IReadOnlyList<CustomerOrder> Orders { get; init; } = [];
    public IReadOnlyList<Product> Products { get; init; } = [];
    public IReadOnlyList<StaffProductReviewView> RecentProductReviews { get; init; } = [];
}

public sealed record StaffProductReviewView(
    int ReviewId,
    int OrderId,
    string CustomerName,
    string ProductName,
    string ProductSlug,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc,
    string? StaffReply,
    string? StaffReplyBy,
    DateTime? StaffRepliedAtUtc);

public sealed class CustomerDashboardViewModel
{
    public ProfileViewModel Profile { get; init; } = new();
    public IReadOnlyList<CustomerOrder> Orders { get; init; } = [];
    public IReadOnlyDictionary<string, ProductReview> ProductReviews { get; init; } = new Dictionary<string, ProductReview>();

    public static string ReviewKey(int orderId, string productSlug) => $"{orderId}:{productSlug}";
}

public sealed record OrderCreatedResponse(int OrderId, decimal Total, string Status, bool CustomerNotificationSent);
