using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Customer)]
[Route("Promotions")]
public sealed class PromotionsController(PromotionService promotions) : Controller
{
    [HttpPost("Preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview([FromBody] PromotionPreviewRequest request, CancellationToken cancellationToken)
    {
        var result = await promotions.PreviewAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { code = result.Code, subtotal = result.Subtotal, discount = result.Discount });
    }
}
