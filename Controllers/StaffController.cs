using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
public sealed class StaffController(
    ApplicationDbContext database,
    UserManager<ApplicationUser> userManager,
    OrderNotificationService notifications,
    IWebHostEnvironment environment,
    IConfiguration configuration) : Controller
{
    private const string ReturnRequestedFilter = "ReturnRequested";
    private static string NormalizeDashboardTab(string? tab) =>
        tab is "support" or "orders" or "stock" or "feedback" ? tab : "overview";

    private IActionResult RedirectToDashboard(string? tab) =>
        RedirectToAction(nameof(Index), new { tab = NormalizeDashboardTab(tab) });

    private string ReturnVideoDirectory => Path.Combine(
        configuration["Storage:PersistentPath"] ?? environment.ContentRootPath,
        "private",
        "return-videos");

    public async Task<IActionResult> Index(string? status = null, string? search = null)
    {
        if (!string.IsNullOrWhiteSpace(status) && status != ReturnRequestedFilter && !OrderStatuses.All.Contains(status, StringComparer.Ordinal))
        {
            return BadRequest();
        }

        search = search?.Trim();
        if (search?.Length > 100)
        {
            return BadRequest();
        }

        var orders = database.Orders.AsNoTracking().Include(order => order.Items).AsQueryable();
        if (status == ReturnRequestedFilter)
        {
            orders = orders.Where(order => order.ReturnRequestedAtUtc != null);
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            orders = orders.Where(order => order.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var hasOrderId = int.TryParse(search, out var orderId);
            orders = orders.Where(order =>
                (hasOrderId && order.Id == orderId) ||
                order.CustomerName.Contains(search) ||
                order.Phone.Contains(search));
        }

        var model = new StaffDashboardViewModel
        {
            PendingOrderCount = await database.Orders.CountAsync(order => order.Status == OrderStatuses.Pending),
            ProcessingOrderCount = await database.Orders.CountAsync(order => order.Status == OrderStatuses.Processing),
            ShippedOrderCount = await database.Orders.CountAsync(order => order.Status == OrderStatuses.Shipped),
            LowStockCount = await database.Products.CountAsync(product => product.IsActive && product.Stock <= 5),
            ProductReviewCount = await database.ProductReviews.CountAsync(),
            StatusFilter = status,
            OrderSearch = search ?? string.Empty,
            Orders = await orders.OrderByDescending(order => order.CreatedAtUtc).Take(100).ToListAsync(),
            Products = await database.Products.AsNoTracking().OrderBy(product => product.Name).ToListAsync(),
            RecentProductReviews = await (
                from review in database.ProductReviews.AsNoTracking()
                join product in database.Products.AsNoTracking() on review.ProductSlug equals product.Slug
                orderby review.CreatedAtUtc descending
                select new StaffProductReviewView(
                    review.Id,
                    review.CustomerOrderId,
                    review.Order.CustomerName,
                    product.Name,
                    review.ProductSlug,
                    review.Rating,
                    review.Comment,
                    review.CreatedAtUtc,
                    review.StaffReply,
                    review.StaffReplyBy,
                    review.StaffRepliedAtUtc))
                .Take(50)
                .ToListAsync()
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> OrderDetails(int id, CancellationToken cancellationToken)
    {
        var order = await database.Orders.AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return order is null ? NotFound() : View(order);
    }

    [HttpGet]
    public async Task<IActionResult> ReturnVideo(int id, CancellationToken cancellationToken)
    {
        var videoId = await database.Orders.AsNoTracking()
            .Where(order => order.Id == id && order.ReturnRequestedAtUtc != null)
            .Select(order => order.ReturnVideoId)
            .FirstOrDefaultAsync(cancellationToken);
        if (videoId is null || !Regex.IsMatch(videoId, "^[a-f0-9]{32}\\.(mp4|mov|webm)$", RegexOptions.CultureInvariant))
        {
            return NotFound();
        }

        var path = Path.Combine(ReturnVideoDirectory, videoId);
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }
        var contentType = Path.GetExtension(videoId) switch
        {
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            _ => "video/mp4"
        };
        return PhysicalFile(path, contentType, enableRangeProcessing: true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyToProductReview(ProductReviewReplyViewModel model, string? tab, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Phản hồi cần có nội dung và không được vượt quá 1.000 ký tự.";
            return RedirectToDashboard(tab);
        }

        var review = await database.ProductReviews.FirstOrDefaultAsync(item => item.Id == model.ReviewId, cancellationToken);
        if (review is null)
        {
            return NotFound();
        }

        var staff = await userManager.GetUserAsync(User);
        if (staff is null)
        {
            return Challenge();
        }

        review.StaffReply = model.Reply.Trim();
        review.StaffReplyBy = staff.DisplayName;
        review.StaffRepliedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);

        TempData["Notice"] = "Đã gửi phản hồi cho khách hàng.";
        return RedirectToDashboard(tab);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(int id, string status, string? tab)
    {
        var order = await database.Orders.Include(item => item.Items).FirstOrDefaultAsync(item => item.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        var allowed = order.Status switch
        {
            OrderStatuses.Pending => new[] { OrderStatuses.Processing, OrderStatuses.Cancelled },
            OrderStatuses.Processing => new[] { OrderStatuses.Shipped, OrderStatuses.Cancelled },
            OrderStatuses.Shipped => new[] { OrderStatuses.Delivered },
            _ => []
        };
        if (!allowed.Contains(status, StringComparer.Ordinal))
        {
            TempData["Error"] = "Không thể chuyển đơn hàng sang trạng thái đã chọn.";
            return RedirectToDashboard(tab);
        }

        await using var transaction = await database.Database.BeginTransactionAsync();
        order.Status = status;
        if (status == OrderStatuses.Cancelled)
        {
            var lines = await database.OrderItems.Where(item => item.CustomerOrderId == id).ToListAsync();
            foreach (var line in lines)
            {
                var product = await database.Products.FirstOrDefaultAsync(item => item.Slug == line.ProductSlug);
                if (product is not null)
                {
                    product.Stock += line.Quantity;
                }
            }
        }
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        var customer = await userManager.FindByIdAsync(order.UserId);
        await notifications.NotifyOrderStatusChangedAsync(order, customer?.Email, CancellationToken.None);
        TempData["Notice"] = "Trạng thái đơn hàng đã được cập nhật.";
        return RedirectToDashboard(tab);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStock(string slug, int stock, string? tab)
    {
        if (stock is < 0 or > 100000)
        {
            TempData["Error"] = "Tồn kho phải nằm trong khoảng 0–100.000.";
            return RedirectToDashboard(tab);
        }

        var product = await database.Products.FirstOrDefaultAsync(item => item.Slug == slug);
        if (product is null)
        {
            return NotFound();
        }

        product.Stock = stock;
        await database.SaveChangesAsync();
        TempData["Notice"] = "Tồn kho đã được cập nhật.";
        return RedirectToDashboard(tab);
    }
}
