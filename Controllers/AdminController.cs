using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminController(
    ApplicationDbContext database,
    UserManager<ApplicationUser> userManager,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<AdminController> logger) : Controller
{
    private string ProductImageDirectory => Path.Combine(
        configuration["Storage:PersistentPath"] ?? environment.WebRootPath,
        "images",
        "uploads");

    private const long MaxProductImageSize = 150L * 1024 * 1024;
    private const long MaxProductImageRequestSize = MaxProductImageSize + 1024 * 1024;
    private const decimal MaxRecordedAmount = 9_999_999_999.99m;
    private static readonly string[] ExpenseCategories =
    [
        "Thuê mặt bằng", "Điện, nước, internet", "Lương nhân viên", "Marketing",
        "Đóng gói & vận chuyển", "Phần mềm & công cụ", "Thuế & phí", "Sửa chữa", "Khác"
    ];

    public async Task<IActionResult> Index()
    {
        var users = await userManager.Users.OrderBy(user => user.Email).ToListAsync();
        var userRows = new List<AdminUserRow>();
        foreach (var user in users)
        {
            var userRoles = await userManager.GetRolesAsync(user);
            userRows.Add(new AdminUserRow(user.Id, user.Email ?? "", user.DisplayName, userRoles.FirstOrDefault() ?? "—"));
        }

        var orders = await database.Orders.AsNoTracking().Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAtUtc).Take(30).ToListAsync();
        var usesSqlServer = database.Database.IsSqlServer();
        var model = new AdminDashboardViewModel
        {
            CustomerCount = userRows.Count(user => user.Role == AppRoles.Customer),
            EmployeeCount = userRows.Count(user => user.Role == AppRoles.Employee),
            ProductCount = await database.Products.CountAsync(product => product.IsActive),
            PendingOrderCount = await database.Orders.CountAsync(order => order.Status == OrderStatuses.Pending),
            SalesTotal = (await database.Orders.Where(order => order.Status != OrderStatuses.Cancelled)
                .Select(order => order.Total).ToListAsync()).Sum(),
            StockPurchaseTotal = (await database.StockReceipts.AsNoTracking().Select(receipt => new { receipt.Quantity, receipt.UnitCost }).ToListAsync()).Sum(receipt => receipt.Quantity * receipt.UnitCost),
            OperatingExpenseTotal = (await database.OperatingExpenses.AsNoTracking().Select(expense => expense.Amount).ToListAsync()).Sum(),
            RecentStockReceipts = await database.StockReceipts.AsNoTracking().OrderByDescending(receipt => receipt.ReceivedOn).ThenByDescending(receipt => receipt.Id).Take(50).ToListAsync(),
            RecentOperatingExpenses = await database.OperatingExpenses.AsNoTracking().OrderByDescending(expense => expense.SpentOn).ThenByDescending(expense => expense.Id).Take(50).ToListAsync(),
            Products = usesSqlServer
                ? await database.Products.AsNoTracking().Include(product => product.ProductCategory).OrderBy(product => product.Name).ToListAsync()
                : await database.Products.AsNoTracking().OrderBy(product => product.Name).ToListAsync(),
            ProductCategories = usesSqlServer
                ? await database.ProductCategories.AsNoTracking().OrderBy(category => category.CategoryName).ToListAsync()
                : [],
            UsesSqlServer = usesSqlServer,
            Users = userRows,
            RecentOrders = orders
        };
        model.RecordedCashFlow = model.SalesTotal - model.StockPurchaseTotal - model.OperatingExpenseTotal;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordStockReceipt(StockReceiptViewModel model)
    {
        if (!ModelState.IsValid || model.ReceivedOn == default || model.ReceivedOn > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)) ||
            model.Quantity * model.UnitCost > MaxRecordedAmount)
        {
            TempData["Error"] = "Thông tin nhập hàng không hợp lệ. Kiểm tra sản phẩm, số lượng, đơn giá và ngày nhập (không vượt quá hôm nay).";
            return RedirectToAction(nameof(Index), new { tab = "expenses" });
        }

        var product = await database.Products.AsNoTracking().FirstOrDefaultAsync(item => item.Slug == model.ProductSlug);
        if (product is null)
        {
            TempData["Error"] = "Không tìm thấy sản phẩm cần nhập kho.";
            return RedirectToAction(nameof(Index), new { tab = "expenses" });
        }

        await using var transaction = await database.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        var updatedRows = await database.Products
            .Where(item => item.Slug == model.ProductSlug && item.Stock <= 100000 - model.Quantity)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.Stock, item => item.Stock + model.Quantity), HttpContext.RequestAborted);
        if (updatedRows != 1)
        {
            await transaction.RollbackAsync(HttpContext.RequestAborted);
            TempData["Error"] = "Không thể nhập số lượng này vì tồn kho sẽ vượt giới hạn cho phép. Hãy kiểm tra tồn kho hiện tại.";
            return RedirectToAction(nameof(Index), new { tab = "expenses" });
        }

        database.StockReceipts.Add(new StockReceipt
        {
            ProductSlug = product.Slug,
            ProductName = product.Name,
            Quantity = model.Quantity,
            UnitCost = model.UnitCost,
            Supplier = model.Supplier.Trim(),
            Reference = model.Reference.Trim(),
            ReceivedOn = model.ReceivedOn,
            Notes = model.Notes.Trim(),
            RecordedBy = userManager.GetUserId(User) ?? throw new InvalidOperationException("The authenticated administrator has no user identifier.")
        });
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        await transaction.CommitAsync(HttpContext.RequestAborted);
        TempData["Notice"] = $"Đã ghi nhận nhập {model.Quantity:N0} sản phẩm {product.Name}; tồn kho đã được cộng thêm.";
        return RedirectToAction(nameof(Index), new { tab = "expenses" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordOperatingExpense(OperatingExpenseViewModel model)
    {
        if (!ModelState.IsValid || !ExpenseCategories.Contains(model.Category, StringComparer.Ordinal) ||
            model.SpentOn == default || model.SpentOn > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
        {
            TempData["Error"] = "Thông tin chi phí không hợp lệ. Hãy chọn nhóm chi phí có sẵn, nhập số tiền và ngày chi không vượt quá hôm nay.";
            return RedirectToAction(nameof(Index), new { tab = "expenses" });
        }

        database.OperatingExpenses.Add(new OperatingExpense
        {
            Category = model.Category,
            Description = model.Description.Trim(),
            Amount = model.Amount,
            Supplier = model.Supplier.Trim(),
            SpentOn = model.SpentOn,
            Notes = model.Notes.Trim(),
            RecordedBy = userManager.GetUserId(User) ?? throw new InvalidOperationException("The authenticated administrator has no user identifier.")
        });
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        TempData["Notice"] = "Đã ghi nhận khoản chi vận hành.";
        return RedirectToAction(nameof(Index), new { tab = "expenses" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee(EmployeeCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin nhân viên không hợp lệ. Hãy kiểm tra các trường bắt buộc và mật khẩu xác nhận.";
            return RedirectToAction(nameof(Index), new { tab = "users" });
        }

        var email = model.Email.Trim();
        var employee = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = model.DisplayName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            EmailConfirmed = true
        };

        await using var transaction = await database.Database.BeginTransactionAsync();
        var createResult = await userManager.CreateAsync(employee, model.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = string.Join(" ", createResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index), new { tab = "users" });
        }

        var roleResult = await userManager.AddToRoleAsync(employee, AppRoles.Employee);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = string.Join(" ", roleResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index), new { tab = "users" });
        }

        await transaction.CommitAsync();
        TempData["Notice"] = $"Đã tạo tài khoản nhân viên {email}. Nhân viên có thể đăng nhập tại trang Đăng nhập.";
        return RedirectToAction(nameof(Index), new { tab = "users" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxProductImageRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxProductImageRequestSize)]
    public async Task<IActionResult> SaveProduct(ProductEditViewModel model)
    {
        if (!database.Database.IsSqlServer())
        {
            ModelState.Remove(nameof(model.CategoryId));
        }

        if (!ModelState.IsValid)
        {
            var validationErrors = ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors.Select(error => (PropertyName: entry.Key, error.ErrorMessage)))
                .Take(5)
                .ToArray();
            logger.LogWarning("Product save rejected because model validation failed: {ValidationErrors}",
                string.Join("; ", validationErrors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}")));

            var details = validationErrors.Select(error =>
            {
                var fieldName = error.PropertyName switch
                {
                    "ImageFile" => "Ảnh sản phẩm",
                    "CategoryId" => "Danh mục dữ liệu",
                    "Slug" => "Mã sản phẩm",
                    "Name" => "Tên sản phẩm",
                    "Price" => "Giá",
                    "SizesCsv" => "Kích cỡ",
                    "Color" => "Màu sắc",
                    "Stock" => "Tồn kho",
                    "IsActive" => "Trạng thái sản phẩm",
                    _ => error.PropertyName
                };
                var message = string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Giá trị không hợp lệ hoặc tệp vượt giới hạn tải lên của máy chủ." : error.ErrorMessage;
                return $"{fieldName}: {message}";
            });
            TempData["Error"] = validationErrors.Length == 0
                ? "Thông tin sản phẩm không hợp lệ. Hãy kiểm tra lại dữ liệu đã nhập."
                : string.Join(" ", details);
            return RedirectToAction(nameof(Index), "Admin", new { area = "", tab = "products" });
        }
        if (model.SizesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length == 0 ||
            model.SizesCsv.Split(',').Any(size => string.IsNullOrWhiteSpace(size) || size.Trim().Length > 30))
        {
            TempData["Error"] = "Hãy nhập ít nhất một kích cỡ hợp lệ, phân cách bằng dấu phẩy.";
            return RedirectToAction(nameof(Index), new { tab = "products" });
        }
        string? imageExtension = null;
        if (model.ImageFile is { Length: > 0 } imageFile)
        {
            if (imageFile.Length > MaxProductImageSize)
            {
                TempData["Error"] = "Ảnh tải lên không được vượt quá 150 MB.";
                return RedirectToAction(nameof(Index), new { tab = "products" });
            }

            imageExtension = await GetImageExtensionAsync(imageFile, HttpContext.RequestAborted);
            if (imageExtension is null)
            {
                TempData["Error"] = "Không nhận diện được định dạng ảnh. Hãy chọn ảnh JPEG, PNG, WebP, GIF, BMP, TIFF, AVIF hoặc HEIF.";
                return RedirectToAction(nameof(Index), new { tab = "products" });
            }
        }

        if (database.Database.IsSqlServer() &&
            !await database.ProductCategories.AnyAsync(category => category.CategoryId == model.CategoryId))
        {
            TempData["Error"] = "Danh mục sản phẩm không tồn tại trong FashionStore.";
            return RedirectToAction(nameof(Index), new { tab = "products" });
        }

        var slug = model.Slug.Trim();
        var product = await database.Products.FirstOrDefaultAsync(item => item.Slug == slug);
        if (product is null && imageExtension is null)
        {
            TempData["Error"] = "Hãy chọn ảnh sản phẩm từ thiết bị của bạn.";
            return RedirectToAction(nameof(Index), new { tab = "products" });
        }

        var previousImageId = product?.ImageId;
        if (product is null)
        {
            product = new Product { Slug = slug };
            database.Products.Add(product);
        }

        if (database.Database.IsSqlServer())
        {
            product.CategoryId = model.CategoryId;
        }
        product.Name = model.Name.Trim();
        product.Category = model.Category;
        product.Price = model.Price;
        var uploadedImageId = imageExtension is not null && model.ImageFile is not null
            ? await SaveProductImageAsync(model.ImageFile, imageExtension, HttpContext.RequestAborted)
            : null;
        if (uploadedImageId is not null)
        {
            product.ImageId = uploadedImageId;
        }
        product.Description = model.Description?.Trim() ?? string.Empty;
        product.SizesCsv = string.Join(',', model.SizesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase));
        product.Color = model.Color.Trim();
        product.Stock = model.Stock;
        product.IsActive = model.IsActive;
        try
        {
            await database.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch
        {
            if (uploadedImageId is not null)
            {
                DeleteProductImage(uploadedImageId);
            }

            throw;
        }

        if (uploadedImageId is not null && previousImageId is not null)
        {
            DeleteProductImage(previousImageId);
        }

        TempData["Notice"] = uploadedImageId is not null
            ? "Sản phẩm đã được lưu và ảnh mới đã được cập nhật."
            : product.ImageId is not null
                ? "Thông tin sản phẩm đã được lưu nhưng máy chủ không nhận được ảnh mới, ảnh cũ vẫn được giữ nguyên. Hãy chọn lại tệp ảnh rồi lưu."
                : "Sản phẩm đã được lưu.";
        return RedirectToAction(nameof(Index), new { tab = "products" });
    }

    private async Task<string?> GetImageExtensionAsync(IFormFile imageFile, CancellationToken cancellationToken)
    {
        var header = new byte[64];
        await using var stream = imageFile.OpenReadStream();
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var read = await stream.ReadAsync(header.AsMemory(bytesRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        if (bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytesRead >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ".png";
        }

        if (bytesRead >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8))
        {
            if (header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            {
                return ".webp";
            }

            if (header.AsSpan(8, 4).SequenceEqual("WAVE"u8))
            {
                return null;
            }
        }

        if (bytesRead >= 6 && (header.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || header.AsSpan(0, 6).SequenceEqual("GIF89a"u8)))
        {
            return ".gif";
        }

        if (bytesRead >= 2 && header.AsSpan(0, 2).SequenceEqual("BM"u8))
        {
            return ".bmp";
        }

        if (bytesRead >= 4 &&
            (header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) ||
             header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }) ||
             header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x49, 0x49, 0x2B, 0x00 }) ||
             header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2B })))
        {
            return ".tiff";
        }

        if (bytesRead >= 12 && header.AsSpan(4, 4).SequenceEqual("ftyp"u8))
        {
            var hasAvifBrand = false;
            var hasHeifBrand = false;
            for (var offset = 8; offset + 4 <= bytesRead; offset += 4)
            {
                var brand = header.AsSpan(offset, 4);
                hasAvifBrand |= brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8);
                hasHeifBrand |= brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) ||
                                 brand.SequenceEqual("hevc"u8) || brand.SequenceEqual("hevx"u8) ||
                                 brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("msf1"u8);
            }

            if (hasAvifBrand)
            {
                return ".avif";
            }

            if (hasHeifBrand)
            {
                return ".heic";
            }
        }

        return null;
    }

    private async Task<string> SaveProductImageAsync(IFormFile imageFile, string extension, CancellationToken cancellationToken)
    {
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var uploadDirectory = ProductImageDirectory;
        Directory.CreateDirectory(uploadDirectory);
        var destination = Path.Combine(uploadDirectory, fileName);
        try
        {
            await using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await imageFile.CopyToAsync(stream, cancellationToken);
            return $"uploads/{fileName}";
        }
        catch
        {
            DeleteProductImage($"uploads/{fileName}");
            throw;
        }
    }

    private void DeleteProductImage(string imageId)
    {
        const string uploadPrefix = "uploads/";
        if (!imageId.StartsWith(uploadPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var fileName = imageId[uploadPrefix.Length..];
        if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, "^[a-f0-9]{32}\\.(jpg|png|webp|gif|bmp|tiff|avif|heic)$"))
        {
            return;
        }

        var path = Path.Combine(ProductImageDirectory, fileName);
        try
        {
            System.IO.File.Delete(path);
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Unable to remove replaced product image {ImageId}", imageId);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Unable to remove replaced product image {ImageId}", imageId);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(string slug)
    {
        var product = await database.Products.FirstOrDefaultAsync(item => item.Slug == slug);
        if (product is null)
        {
            return NotFound();
        }

        product.IsActive = false;
        await database.SaveChangesAsync();
        TempData["Notice"] = "Sản phẩm đã được ẩn khỏi cửa hàng. Dữ liệu đơn hàng cũ được giữ nguyên.";
        return RedirectToAction(nameof(Index), new { tab = "products" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string userId, string role)
    {
        if (!AppRoles.All.Contains(role, StringComparer.Ordinal))
        {
            return BadRequest();
        }

        var target = await userManager.FindByIdAsync(userId);
        if (target is null)
        {
            return NotFound();
        }
        var currentUserId = userManager.GetUserId(User);
        if (target.Id == currentUserId)
        {
            TempData["Error"] = "Không thể tự thay đổi vai trò của chính mình.";
            return RedirectToAction(nameof(Index), new { tab = "users" });
        }

        await using var transaction = await database.Database.BeginTransactionAsync();
        var currentRoles = await userManager.GetRolesAsync(target);
        if (currentRoles.Contains(AppRoles.Admin) && role != AppRoles.Admin)
        {
            var adminUsers = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
            if (adminUsers.Count <= 1)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Không thể hạ quyền quản trị viên cuối cùng.";
                return RedirectToAction(nameof(Index), new { tab = "users" });
            }
        }

        var removeResult = await userManager.RemoveFromRolesAsync(target, currentRoles);
        if (!removeResult.Succeeded)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = string.Join(" ", removeResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index), new { tab = "users" });
        }
        var addResult = await userManager.AddToRoleAsync(target, role);
        if (!addResult.Succeeded)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = string.Join(" ", addResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index), new { tab = "users" });
        }

        await transaction.CommitAsync();
        TempData["Notice"] = "Vai trò tài khoản đã được cập nhật.";
        return RedirectToAction(nameof(Index), new { tab = "users" });
    }
}
