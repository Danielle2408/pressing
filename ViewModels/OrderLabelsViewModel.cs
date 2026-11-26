using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class OrderLabelsViewModel
{
    public LaundryOrder Order { get; init; } = null!;
    public string QrCodeDataUri { get; init; } = string.Empty;
}
