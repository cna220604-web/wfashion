using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Services;

public sealed class PromotionService(ApplicationDbContext database)
{
    public async Task<PromotionValidationResult> ValidateAsync(string code, decimal subtotal, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var promotion = await database.Promotions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.NormalizedCode == normalizedCode, cancellationToken);
        var now = DateTime.UtcNow;
        if (promotion is null || !promotion.IsAvailable(now))
        {
            return PromotionValidationResult.Invalid("Mã giảm giá không tồn tại, đã hết hạn hoặc hết lượt sử dụng.");
        }

        if (subtotal < promotion.MinimumOrderAmount)
        {
            return PromotionValidationResult.Invalid($"Đơn hàng cần đạt {promotion.MinimumOrderAmount:N0}₫ để sử dụng mã này.");
        }

        var discount = promotion.CalculateDiscount(subtotal);
        if (discount <= 0)
        {
            return PromotionValidationResult.Invalid("Mã giảm giá không áp dụng được cho đơn hàng này.");
        }

        return PromotionValidationResult.Valid(promotion, discount);
    }

    public async Task<PromotionPreviewResult> PreviewAsync(PromotionPreviewRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count is < 1 or > 30
            || request.Items.Any(item => string.IsNullOrWhiteSpace(item.ProductSlug) || item.Quantity is < 1 or > 10))
        {
            return PromotionPreviewResult.Invalid("Giỏ hàng không hợp lệ. Hãy tải lại trang và thử lại.");
        }

        var requestedSlugs = request.Items.Select(item => item.ProductSlug.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var products = await database.Products.AsNoTracking()
            .Where(product => requestedSlugs.Contains(product.Slug) && product.IsActive)
            .ToDictionaryAsync(product => product.Slug, StringComparer.OrdinalIgnoreCase, cancellationToken);
        if (products.Count != requestedSlugs.Length)
        {
            return PromotionPreviewResult.Invalid("Một hoặc nhiều sản phẩm không còn được bán. Hãy tải lại giỏ hàng.");
        }

        var subtotal = request.Items.Sum(item => products[item.ProductSlug.Trim()].Price * item.Quantity);
        var validation = await ValidateAsync(request.Code, subtotal, cancellationToken);
        if (!validation.IsValid)
        {
            return PromotionPreviewResult.Invalid(validation.Error!);
        }

        return PromotionPreviewResult.Valid(validation.Promotion!.Code, subtotal, validation.Discount);
    }
}

public sealed record PromotionValidationResult(bool IsValid, Promotion? Promotion, decimal Discount, string? Error)
{
    public static PromotionValidationResult Valid(Promotion promotion, decimal discount) => new(true, promotion, discount, null);
    public static PromotionValidationResult Invalid(string error) => new(false, null, 0, error);
}

public sealed record PromotionPreviewResult(bool IsValid, string? Code, decimal Subtotal, decimal Discount, string? Error)
{
    public static PromotionPreviewResult Valid(string code, decimal subtotal, decimal discount) => new(true, code, subtotal, discount, null);
    public static PromotionPreviewResult Invalid(string error) => new(false, null, 0, 0, error);
}
