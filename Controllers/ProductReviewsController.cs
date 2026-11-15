using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[Authorize(Roles = AppRoles.Customer)]
[Route("ProductReviews")]
public sealed class ProductReviewsController(ApplicationDbContext database, ILogger<ProductReviewsController> logger) : Controller
{
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductReviewSubmissionViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin đánh giá chưa hợp lệ. Vui lòng kiểm tra số sao và nội dung.";
            return RedirectToAction("Customer", "Dashboard");
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var order = await database.Orders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == model.OrderId && item.UserId == userId, cancellationToken);
        if (order is null || order.Status != OrderStatuses.Delivered ||
            !order.Items.Any(item => item.ProductSlug == model.ProductSlug))
        {
            TempData["Error"] = "Chỉ có thể đánh giá sản phẩm thuộc đơn hàng đã giao thành công.";
            return RedirectToAction("Customer", "Dashboard");
        }

        var alreadyReviewed = await database.ProductReviews.AnyAsync(
            review => review.CustomerOrderId == order.Id && review.ProductSlug == model.ProductSlug,
            cancellationToken);
        if (alreadyReviewed)
        {
            TempData["Notice"] = "Bạn đã gửi đánh giá cho sản phẩm này trong đơn hàng.";
            return RedirectToAction("Customer", "Dashboard");
        }

        database.ProductReviews.Add(new ProductReview
        {
            CustomerOrderId = order.Id,
            ProductSlug = model.ProductSlug,
            Rating = model.Rating,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            var duplicateWasSaved = await database.ProductReviews.AsNoTracking().AnyAsync(
                review => review.CustomerOrderId == order.Id && review.ProductSlug == model.ProductSlug,
                cancellationToken);
            if (!duplicateWasSaved)
            {
                throw;
            }

            logger.LogWarning(exception, "A duplicate product review was rejected for order {OrderId} and product {ProductSlug}", order.Id, model.ProductSlug);
            TempData["Notice"] = "Đánh giá cho sản phẩm này đã được ghi nhận trước đó.";
            return RedirectToAction("Customer", "Dashboard");
        }

        TempData["Notice"] = "Cảm ơn bạn đã đánh giá sản phẩm. Đánh giá đã được hiển thị trên cửa hàng.";
        return RedirectToAction("Customer", "Dashboard");
    }
}
