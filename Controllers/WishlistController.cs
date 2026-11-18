using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Customer)]
[Route("Wishlist")]
public sealed class WishlistController(ApplicationDbContext database) : Controller
{
    [HttpPost("Set")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set([FromBody] SetWishlistItemRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { error = "Vui lòng đăng nhập lại để lưu sản phẩm yêu thích." });
        }

        var productSlug = request.ProductSlug.Trim();
        if (request.IsFavorite && !await database.Products.AnyAsync(
                item => item.Slug == productSlug && item.IsActive,
                cancellationToken))
        {
            return NotFound(new { error = "Sản phẩm không còn được bán." });
        }

        var savedItem = await database.WishlistItems.FirstOrDefaultAsync(
            item => item.UserId == userId && item.ProductSlug == productSlug,
            cancellationToken);
        if (request.IsFavorite && savedItem is null)
        {
            database.WishlistItems.Add(new WishlistItem { UserId = userId, ProductSlug = productSlug });
            await database.SaveChangesAsync(cancellationToken);
        }
        else if (!request.IsFavorite && savedItem is not null)
        {
            database.WishlistItems.Remove(savedItem);
            await database.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { productSlug, isFavorite = request.IsFavorite });
    }
}

public sealed class SetWishlistItemRequest
{
    [Required, StringLength(80)]
    public string ProductSlug { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }
}
