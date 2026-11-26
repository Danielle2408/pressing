using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class CustomerPortalViewModel
{
    public Customer Customer { get; init; } = null!;
    public IReadOnlyList<LaundryOrder> Orders { get; init; } = [];
}

public sealed class CustomerOrderDetailsViewModel
{
    public LaundryOrder Order { get; init; } = null!;
}
