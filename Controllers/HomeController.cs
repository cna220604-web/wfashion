using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class HomeController(
        ApplicationDbContext database,
        UserManager<ApplicationUser> userManager,
        FashionMagazineService fashionMagazine) : Controller
    {
        public Task<IActionResult> Index() => RenderStorePageAsync("shop");

        public Task<IActionResult> Shop() => RenderStorePageAsync("shop");

        public Task<IActionResult> Stylist() => RenderStorePageAsync("stylist");

        public Task<IActionResult> Season() => RenderStorePageAsync("season");

        [HttpGet]
        public async Task<IActionResult> Magazine(CancellationToken cancellationToken)
        {
            var model = await fashionMagazine.GetMagazineAsync(cancellationToken);
            return View(model);
        }

        private async Task<IActionResult> RenderStorePageAsync(string page)
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
            var customer = User.IsInRole(AppRoles.Customer) ? await userManager.GetUserAsync(User) : null;
            ViewData["StorePage"] = page;
            ViewData["Title"] = page switch
            {
                "stylist" => "Phối đồ theo màu",
                "season" => "Gợi ý theo mùa",
                _ => "Mua sắm"
            };
            return View("Index", new StorefrontViewModel
            {
                Products = products,
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
