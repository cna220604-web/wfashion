using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminController(
    ApplicationDbContext database,
    UserManager<ApplicationUser> userManager,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<AdminController> logger,
    ObjectMediaStorage objectStorage) : Controller
{
    private string ProductImageDirectory => Path.Combine(
        configuration["Storage:PersistentPath"] ?? environment.WebRootPath,
        "images",
        "uploads");
    private string FashionVideoDirectory => Path.Combine(
        configuration["Storage:PersistentPath"] ?? environment.WebRootPath,
        "videos",
        "fashion");

    private const long MaxProductImageSize = 150L * 1024 * 1024;
    private const long MaxFashionVideoSize = 100L * 1024 * 1024;
    private const long MaxFashionVideoRequestSize = MaxFashionVideoSize + 1024 * 1024;
    private const long MaxProductImageRequestSize = MaxProductImageSize + 1024 * 1024;
    private static readonly string[] ExpenseCategories =
    [
        "Thuê mặt bằng", "Điện, nước, internet", "Lương nhân viên", "Marketing",
        "Đóng gói & vận chuyển", "Phần mềm & công cụ", "Thuế & phí", "Sửa chữa", "Khác"
    ];

    public async Task<IActionResult> Index(string? tab = null)
    {
        tab = tab is "products" or "videos" or "expenses" or "partners" or "users" or "orders" or "promotions" ? tab : "overview";
        var isOverview = tab == "overview";
        var needsProducts = tab is "products" or "videos" or "expenses";
        ViewData["StockReceiptSaved"] = (TempData["StockReceiptSaved"] as string) == "true";

        IReadOnlyList<AdminUserRow> adminUsers = [];
        IReadOnlyList<AdminUserRow> employeeUsers = [];
        IReadOnlyList<AdminUserRow> customerUsers = [];
        var customerCount = 0;
        var employeeCount = 0;
        if (tab == "users")
        {
            adminUsers = await GetAdminUserRowsAsync(AppRoles.Admin);
            employeeUsers = await GetAdminUserRowsAsync(AppRoles.Employee);
            customerUsers = await GetAdminUserRowsAsync(AppRoles.Customer);
        }

        if (isOverview)
        {
            customerCount = (await userManager.GetUsersInRoleAsync(AppRoles.Customer)).Count;
            employeeCount = (await userManager.GetUsersInRoleAsync(AppRoles.Employee)).Count;
        }

        var model = new AdminDashboardViewModel
        {
            CustomerCount = customerCount,
            EmployeeCount = employeeCount,
            ProductCount = isOverview ? await database.Products.CountAsync(product => product.IsActive) : 0,
            LowStockProducts = isOverview
                ? await database.Products.AsNoTracking()
                    .Where(product => product.IsActive && product.Stock <= Product.LowStockThreshold)
                    .OrderBy(product => product.Stock)
                    .ThenBy(product => product.Name)
                    .Take(10)
                    .ToListAsync()
                : [],
            PendingOrderCount = isOverview ? await database.Orders.CountAsync(order => order.Status == OrderStatuses.Pending) : 0,
            SalesTotal = isOverview
                ? (await database.Orders.Where(order => order.Status != OrderStatuses.Cancelled).Select(order => order.Total).ToListAsync()).Sum()
                : 0,
            StockPurchaseTotal = isOverview
                ? (await database.StockReceipts.AsNoTracking().Select(receipt => new { receipt.Quantity, receipt.UnitCost }).ToListAsync()).Sum(receipt => receipt.Quantity * receipt.UnitCost)
                : 0,
            OperatingExpenseTotal = isOverview
                ? (await database.OperatingExpenses.AsNoTracking().Select(expense => expense.Amount).ToListAsync()).Sum()
                : 0,
            RecentStockReceipts = tab == "expenses"
                ? await database.StockReceipts.AsNoTracking().OrderByDescending(receipt => receipt.ReceivedOn).ThenByDescending(receipt => receipt.Id).Take(50).ToListAsync()
                : [],
            BusinessPartners = tab is "expenses" or "partners"
                ? await database.BusinessPartners.AsNoTracking().OrderBy(partner => partner.Name).ToListAsync()
                : [],
            NextStockReceiptNumber = tab == "expenses"
                ? (await database.StockReceiptNumbers.MaxAsync(number => (int?)number.Id) ?? 0) + 1
                : 1,
            RecentOperatingExpenses = tab == "expenses"
                ? await database.OperatingExpenses.AsNoTracking().OrderByDescending(expense => expense.SpentOn).ThenByDescending(expense => expense.Id).Take(50).ToListAsync()
                : [],
            Products = needsProducts ? await database.Products.AsNoTracking().OrderBy(product => product.Name).ToListAsync() : [],
            FashionVideos = tab == "videos"
                ? await database.FashionVideos.AsNoTracking().OrderByDescending(video => video.CreatedAtUtc).ToListAsync()
                : [],
            AdminUsers = adminUsers,
            EmployeeUsers = employeeUsers,
            CustomerUsers = customerUsers,
            RecentOrders = tab == "orders"
                ? await database.Orders.AsNoTracking().Include(order => order.Items).OrderByDescending(order => order.CreatedAtUtc).Take(30).ToListAsync()
                : [],
            Promotions = tab == "promotions"
                ? await database.Promotions.AsNoTracking().OrderByDescending(promotion => promotion.CreatedAtUtc).ToListAsync()
                : []
        };
        model.RecordedCashFlow = model.SalesTotal - model.StockPurchaseTotal - model.OperatingExpenseTotal;
        return View(model);
    }

    private async Task<IReadOnlyList<AdminUserRow>> GetAdminUserRowsAsync(string role)
    {
        var users = await userManager.GetUsersInRoleAsync(role);
        return users
            .OrderBy(user => user.Email)
            .Select(user => new AdminUserRow(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                role,
                user.PhoneNumber,
                user.Address,
                user.Gender,
                user.AvatarImageId))
            .ToArray();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordStockReceipt(StockReceiptBatchViewModel model)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (!ModelState.IsValid)
        {
            var invalidFields = ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .Select(entry => entry.Key switch
                {
                    "PartnerId" => "nhà cung cấp",
                    "ReceivedOn" => "ngày nhập",
                    _ when entry.Key.Contains("ProductSlug", StringComparison.Ordinal) => "mã sản phẩm ở một dòng",
                    _ when entry.Key.Contains("Quantity", StringComparison.Ordinal) => "số lượng ở một dòng",
                    _ when entry.Key.Contains("UnitCost", StringComparison.Ordinal) => "giá nhập ở một dòng",
                    _ when entry.Key.StartsWith("Items", StringComparison.Ordinal) => "danh sách sản phẩm",
                    _ => entry.Key
                })
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToArray();
            logger.LogWarning("Stock receipt rejected by model validation. Invalid fields: {InvalidFields}", string.Join(", ", invalidFields));
            return RejectStockReceipt(invalidFields.Length > 0
                ? $"Không thể nhập hàng: kiểm tra {string.Join(", ", invalidFields)}. Danh sách đã được giữ lại để sửa."
                : "Thông tin nhập hàng không hợp lệ. Danh sách đã được giữ lại để sửa.");
        }

        if (model.Items is null || model.Items.Count == 0)
            return RejectStockReceipt("Phiếu chưa có sản phẩm. Hãy thêm ít nhất một dòng nhập hàng.");
        if (model.ReceivedOn == default)
            return RejectStockReceipt("Hãy chọn ngày nhập hàng.");
        if (model.ReceivedOn > today)
            return RejectStockReceipt("Ngày nhập không được vượt quá hôm nay.");

        var partner = await database.BusinessPartners.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == model.PartnerId && item.IsActive, HttpContext.RequestAborted);
        if (partner is null)
        {
            return RejectStockReceipt("Hãy chọn một đối tác đang hoạt động làm nhà cung cấp. Các dòng sản phẩm đã được giữ lại.");
        }

        var productSlugs = model.Items.Select(item => item.ProductSlug.Trim()).ToArray();
        var products = await database.Products.AsNoTracking()
            .Where(product => productSlugs.Contains(product.Slug))
            .ToDictionaryAsync(product => product.Slug, StringComparer.OrdinalIgnoreCase, HttpContext.RequestAborted);
        if (products.Count != productSlugs.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return RejectStockReceipt("Không tìm thấy một hoặc nhiều mã sản phẩm. Các dòng đã được giữ lại; hãy chọn lại mã đúng trong danh sách.");
        }

        var recordedBy = userManager.GetUserId(User)
            ?? throw new InvalidOperationException("The authenticated administrator has no user identifier.");
        await using var transaction = await database.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        var receiptNumber = new StockReceiptNumber();
        database.StockReceiptNumbers.Add(receiptNumber);
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        var reference = $"PN-{receiptNumber.Id:D6}";
        while (await database.StockReceipts.AnyAsync(receipt => receipt.Reference == reference, HttpContext.RequestAborted))
        {
            receiptNumber = new StockReceiptNumber();
            database.StockReceiptNumbers.Add(receiptNumber);
            await database.SaveChangesAsync(HttpContext.RequestAborted);
            reference = $"PN-{receiptNumber.Id:D6}";
        }

        foreach (var item in model.Items)
        {
            var productSlug = item.ProductSlug.Trim();
            var updatedRows = await database.Products
                .Where(product => product.Slug == productSlug && product.Stock <= 100000 - item.Quantity)
                .ExecuteUpdateAsync(updates => updates.SetProperty(product => product.Stock, product => product.Stock + item.Quantity), HttpContext.RequestAborted);
            if (updatedRows != 1)
            {
                await transaction.RollbackAsync(HttpContext.RequestAborted);
                return RejectStockReceipt($"Không thể nhập {products[productSlug].Name} vì tồn kho sẽ vượt giới hạn 100.000. Không dòng nào được lưu; danh sách vẫn được giữ lại.");
            }

            database.StockReceipts.Add(new StockReceipt
            {
                ProductSlug = productSlug,
                ProductName = products[productSlug].Name,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                Supplier = $"{partner.Code} · {partner.Name}",
                Reference = reference,
                ReceivedOn = model.ReceivedOn,
                Notes = model.Notes.Trim(),
                RecordedBy = recordedBy
            });
        }

        await database.SaveChangesAsync(HttpContext.RequestAborted);
        await transaction.CommitAsync(HttpContext.RequestAborted);
        TempData["Notice"] = $"Đã ghi nhận phiếu {reference} gồm {model.Items.Count} sản phẩm; tồn kho và lịch sử nhập đã được cập nhật.";
        TempData["StockReceiptSaved"] = "true";
        return RedirectToAction(nameof(Index), new { tab = "expenses" });
    }

    private IActionResult RejectStockReceipt(string error)
    {
        TempData["Error"] = error;
        return RedirectToAction(nameof(Index), new { tab = "expenses" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBusinessPartner(BusinessPartnerCreateViewModel model)
    {
        var code = model.Code?.Trim() ?? string.Empty;
        var name = model.Name?.Trim() ?? string.Empty;
        var normalizedCode = code.ToUpperInvariant();
        var normalizedName = name.ToUpperInvariant();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin đối tác chưa hợp lệ. Kiểm tra mã, tên, địa chỉ, số điện thoại và email.";
            return RedirectToAction(nameof(Index), new { tab = "partners" });
        }

        if (await database.BusinessPartners.AnyAsync(partner => partner.NormalizedCode == normalizedCode, HttpContext.RequestAborted))
        {
            TempData["Error"] = "Mã đối tác này đã được sử dụng.";
            return RedirectToAction(nameof(Index), new { tab = "partners" });
        }

        if (await database.BusinessPartners.AnyAsync(partner => partner.NormalizedName == normalizedName, HttpContext.RequestAborted))
        {
            TempData["Error"] = "Tên đối tác này đã tồn tại.";
            return RedirectToAction(nameof(Index), new { tab = "partners" });
        }

        database.BusinessPartners.Add(new BusinessPartner
        {
            Code = code,
            NormalizedCode = normalizedCode,
            Name = name,
            NormalizedName = normalizedName,
            Address = model.Address.Trim(),
            ContactName = model.ContactName?.Trim() ?? string.Empty,
            Phone = model.Phone.Trim(),
            Email = model.Email?.Trim() ?? string.Empty
        });
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        TempData["Notice"] = $"Đã thêm đối tác {code} · {name}. Đối tác đang hoạt động đã có trong lựa chọn nhà cung cấp.";
        return RedirectToAction(nameof(Index), new { tab = "partners" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBusinessPartner(BusinessPartnerCreateViewModel model)
    {
        var partner = await database.BusinessPartners.SingleOrDefaultAsync(item => item.Id == model.Id, HttpContext.RequestAborted);
        if (partner is null) return NotFound();

        var code = model.Code?.Trim() ?? string.Empty;
        var name = model.Name?.Trim() ?? string.Empty;
        var normalizedCode = code.ToUpperInvariant();
        var normalizedName = name.ToUpperInvariant();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin đối tác chưa hợp lệ. Kiểm tra mã, tên, địa chỉ, số điện thoại và email.";
            return RedirectToAction(nameof(Index), new { tab = "partners" });
        }

        if (await database.BusinessPartners.AnyAsync(item => item.Id != model.Id && item.NormalizedCode == normalizedCode, HttpContext.RequestAborted))
        {
            TempData["Error"] = "Mã đối tác này đã được sử dụng.";
            return RedirectToAction(nameof(Index), new { tab = "partners" });
        }

        if (await database.BusinessPartners.AnyAsync(item => item.Id != model.Id && item.NormalizedName == normalizedName, HttpContext.RequestAborted))
        {
            TempData["Error"] = "Tên đối tác này đã tồn tại.";
            return RedirectToAction(nameof(Index), new { tab = "partners" });
        }

        partner.Code = code;
        partner.NormalizedCode = normalizedCode;
        partner.Name = name;
        partner.NormalizedName = normalizedName;
        partner.Address = model.Address.Trim();
        partner.ContactName = model.ContactName?.Trim() ?? string.Empty;
        partner.Phone = model.Phone.Trim();
        partner.Email = model.Email?.Trim() ?? string.Empty;
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        TempData["Notice"] = $"Đã cập nhật đối tác {code} · {name}.";
        return RedirectToAction(nameof(Index), new { tab = "partners" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetBusinessPartnerStatus(int id, bool isActive)
    {
        var partner = await database.BusinessPartners.SingleOrDefaultAsync(item => item.Id == id, HttpContext.RequestAborted);
        if (partner is null)
        {
            return NotFound();
        }

        partner.IsActive = isActive;
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        TempData["Notice"] = isActive ? $"Đã bật lại đối tác {partner.Name}." : $"Đã ngừng sử dụng đối tác {partner.Name}; lịch sử nhập hàng được giữ nguyên.";
        return RedirectToAction(nameof(Index), new { tab = "partners" });
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
    public async Task<IActionResult> CreatePromotion(PromotionCreateViewModel model)
    {
        var code = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (model.Type is not (PromotionTypes.Percentage or PromotionTypes.FixedAmount)
            || (model.Type == PromotionTypes.Percentage && model.Value > 100)
            || (model.Type == PromotionTypes.FixedAmount && model.Value < 1000)
            || (model.Type == PromotionTypes.FixedAmount && model.MaximumDiscountAmount is not null)
            || model.StartsOn == default
            || model.EndsOn == DateOnly.MaxValue
            || (model.EndsOn is not null && model.EndsOn < model.StartsOn)
            || !ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin voucher không hợp lệ. Hãy kiểm tra mã, mức giảm, điều kiện đơn tối thiểu và thời hạn.";
            return RedirectToAction(nameof(Index), new { tab = "promotions" });
        }

        if (await database.Promotions.AnyAsync(item => item.NormalizedCode == code, HttpContext.RequestAborted))
        {
            TempData["Error"] = "Mã voucher này đã tồn tại.";
            return RedirectToAction(nameof(Index), new { tab = "promotions" });
        }

        var startsAtUtc = new DateTimeOffset(model.StartsOn.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).UtcDateTime;
        DateTime? endsAtUtc = model.EndsOn is null
            ? null
            : new DateTimeOffset(model.EndsOn.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).UtcDateTime;
        database.Promotions.Add(new Promotion
        {
            Code = code,
            NormalizedCode = code,
            Type = model.Type,
            Value = model.Value,
            MinimumOrderAmount = model.MinimumOrderAmount,
            MaximumDiscountAmount = model.MaximumDiscountAmount,
            MaximumRedemptions = model.MaximumRedemptions,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            IsActive = true
        });
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        TempData["Notice"] = $"Đã tạo voucher {code}. Khách hàng có thể áp dụng tại giỏ hàng.";
        return RedirectToAction(nameof(Index), new { tab = "promotions" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPromotionVisibility(int id, bool isActive)
    {
        var promotion = await database.Promotions.FirstOrDefaultAsync(item => item.Id == id, HttpContext.RequestAborted);
        if (promotion is null)
        {
            return NotFound();
        }

        promotion.IsActive = isActive;
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        TempData["Notice"] = isActive ? $"Voucher {promotion.Code} đã được bật." : $"Voucher {promotion.Code} đã được tắt.";
        return RedirectToAction(nameof(Index), new { tab = "promotions" });
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
    [RequestSizeLimit(MaxFashionVideoRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFashionVideoRequestSize)]
    public async Task<IActionResult> UploadFashionVideo(FashionVideoUploadViewModel model)
    {
        if (!ModelState.IsValid || model.VideoFile is not { Length: > 0 } videoFile)
        {
            TempData["Error"] = "Chọn video, sản phẩm và nhập chú thích hợp lệ trước khi tải lên.";
            return RedirectToAction(nameof(Index), new { tab = "videos" });
        }

        if (videoFile.Length > MaxFashionVideoSize)
        {
            TempData["Error"] = "Video không được vượt quá 100 MB.";
            return RedirectToAction(nameof(Index), new { tab = "videos" });
        }

        var product = await database.Products.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Slug == model.ProductSlug && item.IsActive);
        if (product is null)
        {
            TempData["Error"] = "Hãy chọn một sản phẩm đang hiển thị trên cửa hàng.";
            return RedirectToAction(nameof(Index), new { tab = "videos" });
        }

        var extension = await GetFashionVideoExtensionAsync(videoFile, HttpContext.RequestAborted);
        if (extension is null)
        {
            TempData["Error"] = "Định dạng video không hợp lệ. Hỗ trợ video MP4 hoặc WebM.";
            return RedirectToAction(nameof(Index), new { tab = "videos" });
        }

        var fileName = await SaveFashionVideoAsync(videoFile, extension, HttpContext.RequestAborted);
        database.FashionVideos.Add(new FashionVideo
        {
            VideoFileName = fileName,
            ProductSlug = product.Slug,
            Caption = model.Caption.Trim()
        });
        try
        {
            await database.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch
        {
            await DeleteFashionVideoFileAsync(fileName, CancellationToken.None);
            throw;
        }

        TempData["Notice"] = $"Đã đăng video cho sản phẩm {product.Name}.";
        return RedirectToAction(nameof(Index), new { tab = "videos" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetFashionVideoVisibility(int id, bool isActive)
    {
        var video = await database.FashionVideos.FirstOrDefaultAsync(item => item.Id == id);
        if (video is null)
        {
            return NotFound();
        }

        video.IsActive = isActive;
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        TempData["Notice"] = isActive ? "Video đã được hiển thị trên cửa hàng." : "Video đã được ẩn khỏi cửa hàng.";
        return RedirectToAction(nameof(Index), new { tab = "videos" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFashionVideo(int id)
    {
        var video = await database.FashionVideos.FirstOrDefaultAsync(item => item.Id == id);
        if (video is null)
        {
            return NotFound();
        }

        database.FashionVideos.Remove(video);
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        await DeleteFashionVideoFileAsync(video.VideoFileName, HttpContext.RequestAborted);
        TempData["Notice"] = "Video đã được xóa.";
        return RedirectToAction(nameof(Index), new { tab = "videos" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxProductImageRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxProductImageRequestSize)]
    public async Task<IActionResult> SaveProduct(ProductEditViewModel model)
    {
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

    private static async Task<string?> GetFashionVideoExtensionAsync(IFormFile videoFile, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = videoFile.OpenReadStream();
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

        if (bytesRead >= 12 && header.AsSpan(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = header.AsSpan(8, 4);
            if (brand.SequenceEqual("isom"u8) || brand.SequenceEqual("iso2"u8) ||
                brand.SequenceEqual("mp41"u8) || brand.SequenceEqual("mp42"u8) ||
                brand.SequenceEqual("avc1"u8) || brand.SequenceEqual("M4V "u8) ||
                brand.SequenceEqual("MSNV"u8))
            {
                return ".mp4";
            }
        }

        if (bytesRead >= 4 && header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
        {
            return ".webm";
        }

        return null;
    }

    private async Task<string> SaveFashionVideoAsync(IFormFile videoFile, string extension, CancellationToken cancellationToken)
    {
        var fileName = $"{Guid.NewGuid():N}{extension}";
        if (objectStorage.IsConfigured)
        {
            await using var uploadStream = videoFile.OpenReadStream();
            var contentType = extension == ".webm" ? "video/webm" : "video/mp4";
            await objectStorage.UploadAsync($"videos/fashion/{fileName}", uploadStream, videoFile.Length, contentType, cancellationToken);
            return fileName;
        }

        Directory.CreateDirectory(FashionVideoDirectory);
        var destination = Path.Combine(FashionVideoDirectory, fileName);
        try
        {
            await using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await videoFile.CopyToAsync(stream, cancellationToken);
            return fileName;
        }
        catch
        {
            await DeleteFashionVideoFileAsync(fileName, cancellationToken);
            throw;
        }
    }

    private async Task DeleteFashionVideoFileAsync(string fileName, CancellationToken cancellationToken)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, "^[a-f0-9]{32}\\.(mp4|webm)$"))
        {
            return;
        }

        try
        {
            if (objectStorage.IsConfigured)
            {
                await objectStorage.DeleteAsync($"videos/fashion/{fileName}", cancellationToken);
            }
            else
            {
                System.IO.File.Delete(Path.Combine(FashionVideoDirectory, fileName));
            }
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Unable to remove fashion video file {VideoFileName}", fileName);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Unable to remove fashion video file {VideoFileName}", fileName);
        }
        catch (AmazonS3Exception exception)
        {
            logger.LogWarning(exception, "Unable to remove fashion video object {VideoFileName}", fileName);
        }
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
    public async Task<IActionResult> SetRole(string userId, string role, string? userGroup)
    {
        userGroup = userGroup is "admin" or "employee" or "customer" ? userGroup : "admin";
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
            return RedirectToAction(nameof(Index), new { tab = "users", userGroup });
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
                return RedirectToAction(nameof(Index), new { tab = "users", userGroup });
            }
        }

        var removeResult = await userManager.RemoveFromRolesAsync(target, currentRoles);
        if (!removeResult.Succeeded)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = string.Join(" ", removeResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index), new { tab = "users", userGroup });
        }
        var addResult = await userManager.AddToRoleAsync(target, role);
        if (!addResult.Succeeded)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = string.Join(" ", addResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index), new { tab = "users", userGroup });
        }

        await transaction.CommitAsync();
        TempData["Notice"] = "Vai trò tài khoản đã được cập nhật.";
        return RedirectToAction(nameof(Index), new { tab = "users", userGroup });
    }
}
