namespace Pressing.Models;

public sealed class Customer
{
    public int Id { get; set; }
    public string? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NormalizedPhone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int LoyaltyPoints { get; set; }
    public bool ReceiveStatusEmails { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<LaundryOrder> Orders { get; set; } = new List<LaundryOrder>();
    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
}
