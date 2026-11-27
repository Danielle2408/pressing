using System.ComponentModel.DataAnnotations;

namespace Pressing.ViewModels;

public sealed class PaymentContactViewModel
{
    public int OrderId { get; set; }
    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }
    [Phone, StringLength(32)]
    public string? PhoneNumber { get; set; }
}
