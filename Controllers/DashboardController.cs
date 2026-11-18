using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[Authorize]
public sealed class DashboardController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext database,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<DashboardController> logger,
    AvatarObjectStorage avatarStorage) : Controller
{
    private string AvatarDirectory => Path.Combine(
        configuration["Storage:PersistentPath"] ?? environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
        "images",
        "avatars");

    private const long MaxAvatarSize = 10L * 1024 * 1024;
    private const long MaxAvatarRequestSize = MaxAvatarSize + 1024 * 1024;

    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        if (await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            return RedirectToAction("Index", "Admin");
        }
        if (await userManager.IsInRoleAsync(user, AppRoles.Employee))
        {
            return RedirectToAction("Index", "Staff");
        }

        return RedirectToAction(nameof(Customer));
    }

    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> Customer()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var orders = await database.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.UserId == user.Id)
            .OrderByDescending(order => order.CreatedAtUtc)
            .ToListAsync();
        var savedProducts = await database.WishlistItems.AsNoTracking()
            .Where(item => item.UserId == user.Id)
            .Join(database.Products.AsNoTracking().Where(product => product.IsActive),
                item => item.ProductSlug,
                product => product.Slug,
                (_, product) => product)
            .OrderBy(product => product.Name)
            .ToListAsync();
        var orderIds = orders.Select(order => order.Id).ToArray();
        var productReviews = await database.ProductReviews.AsNoTracking()
            .Where(review => orderIds.Contains(review.CustomerOrderId))
            .ToDictionaryAsync(review => CustomerDashboardViewModel.ReviewKey(review.CustomerOrderId, review.ProductSlug));
        var model = new CustomerDashboardViewModel
        {
            Profile = new ProfileViewModel
            {
                DisplayName = user.DisplayName,
                PhoneNumber = user.PhoneNumber,
                Gender = user.Gender,
                Address = user.Address,
                PhoneNumberIsPublic = user.PhoneNumberIsPublic,
                AvatarImageId = user.AvatarImageId
            },
            Orders = orders,
            SavedProducts = savedProducts,
            ProductReviews = productReviews
        };
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Customer)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxAvatarRequestSize)]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        model.AvatarImageId = user.AvatarImageId;
        string? avatarExtension = null;
        if (model.AvatarFile is { Length: > 0 } avatarFile)
        {
            if (avatarFile.Length > MaxAvatarSize)
            {
                ModelState.AddModelError("Profile.AvatarFile", "Ảnh đại diện không được vượt quá 10 MB.");
            }
            else
            {
                avatarExtension = await GetAvatarExtensionAsync(avatarFile, HttpContext.RequestAborted);
                if (avatarExtension is null)
                {
                    ModelState.AddModelError("Profile.AvatarFile", "Ảnh đại diện phải là tệp JPEG, PNG, WebP hoặc GIF hợp lệ.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            return await CustomerWithErrors(model);
        }

        string? uploadedAvatarId = null;
        if (model.AvatarFile is { Length: > 0 } file && avatarExtension is not null)
        {
            uploadedAvatarId = await SaveAvatarAsync(file, avatarExtension, HttpContext.RequestAborted);
        }

        var previousAvatarId = user.AvatarImageId;
        user.DisplayName = model.DisplayName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        user.Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender.Trim();
        user.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
        user.PhoneNumberIsPublic = user.PhoneNumber is not null && model.PhoneNumberIsPublic;
        if (uploadedAvatarId is not null)
        {
            user.AvatarImageId = uploadedAvatarId;
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            if (uploadedAvatarId is not null)
            {
                await DeleteAvatarAsync(uploadedAvatarId, CancellationToken.None);
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return await CustomerWithErrors(model);
        }

        if (uploadedAvatarId is not null && previousAvatarId is not null)
        {
            await DeleteAvatarAsync(previousAvatarId, HttpContext.RequestAborted);
        }

        TempData["Notice"] = "Hồ sơ cá nhân đã được cập nhật.";
        return RedirectToAction(nameof(Customer));
    }

    private async Task<string?> GetAvatarExtensionAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
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
        if (bytesRead >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }
        if (bytesRead >= 6 && (header.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || header.AsSpan(0, 6).SequenceEqual("GIF89a"u8)))
        {
            return ".gif";
        }

        return null;
    }

    private async Task<string> SaveAvatarAsync(IFormFile file, string extension, CancellationToken cancellationToken)
    {
        var fileName = $"{Guid.NewGuid():N}{extension}";
        if (avatarStorage.IsConfigured)
        {
            await using var uploadStream = file.OpenReadStream();
            var contentType = extension switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };
            return await avatarStorage.UploadAsync(fileName, uploadStream, file.Length, contentType, cancellationToken);
        }

        var avatarDirectory = AvatarDirectory;
        Directory.CreateDirectory(avatarDirectory);
        var destination = Path.Combine(avatarDirectory, fileName);
        try
        {
            await using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await file.CopyToAsync(stream, cancellationToken);
            return $"avatars/{fileName}";
        }
        catch
        {
            DeleteLocalAvatar($"avatars/{fileName}");
            throw;
        }
    }

    private async Task DeleteAvatarAsync(string? avatarImageId, CancellationToken cancellationToken)
    {
        if (avatarStorage.IsSupabaseAvatar(avatarImageId))
        {
            if (!avatarStorage.IsConfigured || avatarImageId is null)
            {
                return;
            }

            try
            {
                await avatarStorage.DeleteAsync(avatarImageId, cancellationToken);
            }
            catch (AmazonS3Exception exception)
            {
                logger.LogWarning(exception, "Unable to remove replaced customer avatar {AvatarImageId}", avatarImageId);
            }
            return;
        }

        DeleteLocalAvatar(avatarImageId);
    }

    private void DeleteLocalAvatar(string? avatarImageId)
    {
        const string prefix = "avatars/";
        if (string.IsNullOrWhiteSpace(avatarImageId) || !avatarImageId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var fileName = avatarImageId[prefix.Length..];
        if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, "^[a-f0-9]{32}\\.(jpg|png|webp|gif)$"))
        {
            return;
        }

        var path = Path.Combine(AvatarDirectory, fileName);
        try
        {
            System.IO.File.Delete(path);
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Unable to remove replaced customer avatar {AvatarImageId}", avatarImageId);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Unable to remove replaced customer avatar {AvatarImageId}", avatarImageId);
        }
    }

    private async Task<IActionResult> CustomerWithErrors(ProfileViewModel profile)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        profile.AvatarImageId = user.AvatarImageId;
        ViewData["ProfileEditMode"] = true;
        return View("Customer", new CustomerDashboardViewModel
        {
            Profile = profile,
            Orders = await database.Orders.AsNoTracking().Include(order => order.Items)
                .Where(order => order.UserId == user.Id)
                .OrderByDescending(order => order.CreatedAtUtc)
                .ToListAsync(),
            SavedProducts = await database.WishlistItems.AsNoTracking()
                .Where(item => item.UserId == user.Id)
                .Join(database.Products.AsNoTracking().Where(product => product.IsActive),
                    item => item.ProductSlug,
                    product => product.Slug,
                    (_, product) => product)
                .OrderBy(product => product.Name)
                .ToListAsync()
        });
    }
}
