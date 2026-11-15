using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Customer)]
[Route("Orders")]
public sealed class OrdersController(
    ApplicationDbContext database,
    UserManager<ApplicationUser> userManager,
    OrderNotificationService notifications,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<OrdersController> logger) : Controller
{
    private const long MaxReturnVideoSize = 100L * 1024 * 1024;
    private const long MaxReturnRequestSize = MaxReturnVideoSize + 1024 * 1024;
    private string ReturnVideoDirectory => Path.Combine(
        configuration["Storage:PersistentPath"] ?? environment.ContentRootPath,
        "private",
        "return-videos");

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CheckoutRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { error = "Vui lòng đăng nhập lại để đặt hàng." });
        }

        if (request.Items.Count > 30)
        {
            return BadRequest(new { error = "Đơn hàng có quá nhiều dòng sản phẩm." });
        }

        if (request.Items.Any(item => string.IsNullOrWhiteSpace(item.ProductSlug) || string.IsNullOrWhiteSpace(item.Size)))
        {
            return BadRequest(new { error = "Vui lòng chọn sản phẩm và kích cỡ hợp lệ cho tất cả mặt hàng." });
        }

        var customer = await userManager.FindByIdAsync(userId);
        if (customer is null)
        {
            return Unauthorized(new { error = "Không tìm thấy tài khoản khách hàng. Vui lòng đăng nhập lại." });
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var slugs = request.Items.Select(item => item.ProductSlug).Distinct().ToList();
        var products = await database.Products.Where(product => slugs.Contains(product.Slug) && product.IsActive)
            .ToDictionaryAsync(product => product.Slug, cancellationToken);
        if (products.Count != slugs.Count)
        {
            return BadRequest(new { error = "Một hoặc nhiều sản phẩm không còn được bán. Hãy tải lại trang." });
        }

        foreach (var line in request.Items)
        {
            if (!products.TryGetValue(line.ProductSlug, out var product) || !product.Sizes.Contains(line.Size, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Kích cỡ hoặc sản phẩm không hợp lệ." });
            }
        }

        foreach (var group in request.Items.GroupBy(item => item.ProductSlug))
        {
            var product = products[group.Key];
            var requestedQuantity = group.Sum(item => item.Quantity);
            if (requestedQuantity > product.Stock)
            {
                return Conflict(new { error = $"Sản phẩm {product.Name} không đủ tồn kho." });
            }
            product.Stock -= requestedQuantity;
        }

        var orderItems = request.Items.Select(line =>
        {
            var product = products[line.ProductSlug];
            return new OrderItem
            {
                ProductSlug = product.Slug,
                ProductName = product.Name,
                Size = line.Size,
                UnitPrice = product.Price,
                Quantity = line.Quantity
            };
        }).ToList();
        var subtotal = orderItems.Sum(item => item.UnitPrice * item.Quantity);
        var shippingFee = subtotal >= 800000m ? 0m : 30000m;
        var order = new CustomerOrder
        {
            UserId = userId,
            CustomerName = request.CustomerName.Trim(),
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Status = OrderStatuses.Pending,
            PaymentMethod = "COD",
            Subtotal = subtotal,
            ShippingFee = shippingFee,
            Total = subtotal + shippingFee,
            CreatedAtUtc = DateTime.UtcNow,
            Items = orderItems
        };

        customer.Address = request.Address.Trim();
        database.Orders.Add(order);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var notificationResult = await notifications.NotifyOrderPlacedAsync(order, customer.Email, CancellationToken.None);
        return Ok(new OrderCreatedResponse(order.Id, order.Total, order.Status, notificationResult.CustomerNotified));
    }

    [HttpPost("ConfirmReceived/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmReceived(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var order = await database.Orders.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }
        if (order.Status != OrderStatuses.Shipped)
        {
            TempData["Error"] = "Chỉ có thể xác nhận đã nhận đối với đơn đang giao.";
            return RedirectToAction("Customer", "Dashboard");
        }

        order.Status = OrderStatuses.Delivered;
        order.ReceivedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        var customer = await userManager.FindByIdAsync(userId!);
        await notifications.NotifyOrderStatusChangedAsync(order, customer?.Email, CancellationToken.None);
        TempData["Notice"] = "Bạn đã xác nhận nhận được hàng. Cảm ơn bạn!";
        return RedirectToAction("Customer", "Dashboard");
    }

    [HttpPost("RequestReturn/{id:int}")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxReturnRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxReturnRequestSize)]
    public async Task<IActionResult> RequestReturn(int id, [FromForm] string? reason, [FromForm] IFormFile? video, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var order = await database.Orders.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }
        if (order.Status != OrderStatuses.Delivered || order.ReturnRequestedAtUtc is not null)
        {
            TempData["Error"] = "Chỉ có thể gửi yêu cầu hoàn trả một lần cho đơn đã giao.";
            return RedirectToAction("Customer", "Dashboard");
        }

        reason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length is < 10 or > 1000)
        {
            TempData["Error"] = "Lý do hoàn trả cần từ 10 đến 1.000 ký tự.";
            return RedirectToAction("Customer", "Dashboard");
        }
        if (video is not { Length: > 0 } || video.Length > MaxReturnVideoSize)
        {
            TempData["Error"] = "Vui lòng tải video MP4, MOV hoặc WebM không quá 100 MB.";
            return RedirectToAction("Customer", "Dashboard");
        }

        var extension = await GetReturnVideoExtensionAsync(video, cancellationToken);
        if (extension is null)
        {
            TempData["Error"] = "Tệp tải lên không phải video MP4, MOV hoặc WebM hợp lệ.";
            return RedirectToAction("Customer", "Dashboard");
        }

        var videoId = $"{Guid.NewGuid():N}{extension}";
        var directory = ReturnVideoDirectory;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, videoId);
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await video.CopyToAsync(stream, cancellationToken);
        }

        order.ReturnRequestedAtUtc = DateTime.UtcNow;
        order.ReturnReason = reason;
        order.ReturnVideoId = videoId;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            DeleteReturnVideo(path);
            throw;
        }

        TempData["Notice"] = "Đã gửi yêu cầu hoàn trả. Nhân viên sẽ kiểm tra thông tin và video của bạn.";
        return RedirectToAction("Customer", "Dashboard");
    }

    [HttpPost("Cancel/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var order = await database.Orders.Include(item => item.Items).FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
        if (order is null)
        {
            return NotFound();
        }
        if (order.Status != OrderStatuses.Pending)
        {
            TempData["Error"] = "Chỉ có thể hủy đơn hàng đang chờ xác nhận.";
            return RedirectToAction("Customer", "Dashboard");
        }

        await using var transaction = await database.Database.BeginTransactionAsync();
        order.Status = OrderStatuses.Cancelled;
        var items = await database.OrderItems.Where(item => item.CustomerOrderId == id).ToListAsync();
        foreach (var line in items)
        {
            var product = await database.Products.FirstOrDefaultAsync(item => item.Slug == line.ProductSlug);
            if (product is not null)
            {
                product.Stock += line.Quantity;
            }
        }
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        var customer = await userManager.FindByIdAsync(userId!);
        await notifications.NotifyOrderStatusChangedAsync(order, customer?.Email, CancellationToken.None);
        TempData["Notice"] = "Đơn hàng đã được hủy.";
        return RedirectToAction("Customer", "Dashboard");
    }

    private static async Task<string?> GetReturnVideoExtensionAsync(IFormFile video, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = video.OpenReadStream();
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var read = await stream.ReadAsync(header.AsMemory(bytesRead), cancellationToken);
            if (read == 0) break;
            bytesRead += read;
        }

        if (bytesRead >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
        {
            return header[8] == 0x71 && header[9] == 0x74 ? ".mov" : ".mp4";
        }
        if (bytesRead >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
        {
            return ".webm";
        }
        return null;
    }

    private void DeleteReturnVideo(string path)
    {
        try
        {
            System.IO.File.Delete(path);
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Unable to remove unreferenced return video {ReturnVideoPath}", path);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Unable to remove unreferenced return video {ReturnVideoPath}", path);
        }
    }
}
