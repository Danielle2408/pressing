using System.ComponentModel.DataAnnotations;
using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class PromotionListViewModel
{
    public IReadOnlyList<Promotion> Promotions { get; init; } = [];
    public IReadOnlyList<GarmentCategory> Categories { get; init; } = [];
}

public sealed class CreatePromotionViewModel
{
    [Required, RegularExpression("^[A-Za-z0-9-]{3,32}$", ErrorMessage = "Utilise 3 à 32 lettres, chiffres ou traits d’union.")]
    public string Code { get; set; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 3)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;
    [EnumDataType(typeof(PromotionKind))]
    [Display(Name = "Type de réduction")]
    public PromotionKind Kind { get; set; }
    [Range(typeof(decimal), "0.01", "100000000")]
    [Display(Name = "Valeur")]
    public decimal Value { get; set; }
    [Display(Name = "Catégorie concernée (facultatif)")]
    public int? GarmentCategoryId { get; set; }
    [Required]
    [Display(Name = "Début")]
    public DateTime StartsAtLocal { get; set; } = DateTime.Now;
    [Required]
    [Display(Name = "Fin")]
    public DateTime EndsAtLocal { get; set; } = DateTime.Now.AddDays(7);
}
