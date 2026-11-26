using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.Models;

namespace Pressing.Services;

public sealed record PromotionApplication(Promotion? Promotion, decimal DiscountAmount, string? Error);

public sealed class PromotionPricingService(ApplicationDbContext database)
{
    public async Task<PromotionApplication> ApplyAsync(string? code, IReadOnlyDictionary<int, decimal> categoryAmounts)
    {
        if (string.IsNullOrWhiteSpace(code)) return new PromotionApplication(null, 0m, null);
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length > 32) return new PromotionApplication(null, 0m, "Le code promotionnel est invalide.");
        var now = DateTime.UtcNow;
        var promotion = await database.Promotions.Include(item => item.GarmentCategory)
            .FirstOrDefaultAsync(item => item.NormalizedCode == normalized && item.IsActive &&
                item.StartsAtUtc <= now && item.EndsAtUtc > now &&
                (item.GarmentCategoryId == null || (item.GarmentCategory != null && item.GarmentCategory.IsActive)));
        if (promotion is null) return new PromotionApplication(null, 0m, "Ce code n’existe pas ou n’est pas actif.");
        var eligibleAmount = promotion.GarmentCategoryId.HasValue
            ? categoryAmounts.GetValueOrDefault(promotion.GarmentCategoryId.Value)
            : categoryAmounts.Values.Sum();
        if (eligibleAmount <= 0) return new PromotionApplication(null, 0m, "Aucun article de cette commande ne correspond à ce code.");
        var discount = promotion.Kind == PromotionKind.Percentage
            ? eligibleAmount * promotion.Value / 100m
            : promotion.Value;
        discount = decimal.Round(Math.Min(discount, eligibleAmount), 2);
        return new PromotionApplication(promotion, discount, null);
    }
}
