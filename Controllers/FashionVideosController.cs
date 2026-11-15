using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public sealed class FashionVideosController(ApplicationDbContext database) : Controller
{
    public async Task<IActionResult> Index()
    {
        var videos = await (
            from video in database.FashionVideos.AsNoTracking().Where(video => video.IsActive)
            join product in database.Products.AsNoTracking().Where(product => product.IsActive)
                on video.ProductSlug equals product.Slug
            orderby video.CreatedAtUtc descending
            select new StorefrontFashionVideo(video.Id, video.VideoFileName, product.Slug, product.Name, product.Price, video.Caption))
            .ToListAsync(HttpContext.RequestAborted);

        return View(videos);
    }
}
