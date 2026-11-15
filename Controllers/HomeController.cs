using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class HomeController(ApplicationDbContext database, UserManager<ApplicationUser> userManager) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var products = await database.Products.AsNoTracking()
                .Where(product => product.IsActive)
                .OrderBy(product => product.CreatedAtUtc)
                .ToListAsync();
            var reviewSummaries = await (
                from review in database.ProductReviews.AsNoTracking()
                join product in database.Products.AsNoTracking().Where(product => product.IsActive)
                    on review.ProductSlug equals product.Slug
                group review by review.ProductSlug into productReviews
                select new
                {
                    ProductSlug = productReviews.Key,
                    AverageRating = productReviews.Average(review => review.Rating),
                    ReviewCount = productReviews.Count()
                })
                .ToDictionaryAsync(
                    summary => summary.ProductSlug,
                    summary => new ProductReviewSummary(summary.AverageRating, summary.ReviewCount));
            var fashionVideos = await (
                from video in database.FashionVideos.AsNoTracking().Where(video => video.IsActive)
                join product in database.Products.AsNoTracking().Where(product => product.IsActive)
                    on video.ProductSlug equals product.Slug
                orderby video.CreatedAtUtc descending
                select new StorefrontFashionVideo(video.Id, video.VideoFileName, product.Slug, product.Name, product.Price, video.Caption))
                .ToListAsync();
            var customer = User.IsInRole(AppRoles.Customer) ? await userManager.GetUserAsync(User) : null;
            return View(new StorefrontViewModel
            {
                Products = products,
                FashionVideos = fashionVideos,
                Reviews = reviewSummaries,
                CustomerName = customer?.DisplayName ?? string.Empty,
                CustomerPhone = customer?.PhoneNumber ?? string.Empty,
                CustomerAddress = customer?.Address ?? string.Empty
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
