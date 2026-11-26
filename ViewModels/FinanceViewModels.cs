using System.ComponentModel.DataAnnotations;
using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class CashClosureViewModel
{
    public DateOnly BusinessDate { get; init; }
    public decimal ExpectedCash { get; init; }
    public decimal OtherPayments { get; init; }
    public int PaymentCount { get; init; }
    public CashClosure? Closure { get; init; }
    public decimal CountedCash { get; set; }
    [StringLength(500)]
    public string? Note { get; set; }
}

public sealed class CashClosureHistoryViewModel
{
    public IReadOnlyList<CashClosure> Closures { get; init; } = [];
}

public sealed class CreateExpenseViewModel
{
    [Required, StringLength(160, MinimumLength = 2)]
    [Display(Name = "Libellé")]
    public string Label { get; set; } = string.Empty;
    [Required, StringLength(80, MinimumLength = 2)]
    [Display(Name = "Catégorie")]
    public string Category { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "100000000")]
    [Display(Name = "Montant (FCFA)")]
    public decimal Amount { get; set; }
    [Required]
    [Display(Name = "Date de la dépense")]
    public DateOnly SpentOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}

public sealed class FinanceReportViewModel
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public decimal Revenue { get; init; }
    public decimal Expenses { get; init; }
    public IReadOnlyList<PaymentMethodTotalViewModel> Methods { get; init; } = [];
    public IReadOnlyList<Expense> ExpenseItems { get; init; } = [];
    public CreateExpenseViewModel NewExpense { get; init; } = new();
}

public sealed class PaymentMethodTotalViewModel
{
    public PaymentMethod Method { get; init; }
    public decimal Amount { get; init; }
    public int Count { get; init; }
}
