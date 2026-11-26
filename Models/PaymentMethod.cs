namespace Pressing.Models;

public enum PaymentMethod
{
    Cash,
    MobileMoney,
    Card,
    BankTransfer,
    Notchpay
}

public static class PaymentMethodLabels
{
    public static string ToFrench(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Espèces",
        PaymentMethod.MobileMoney => "Mobile Money reçu",
        PaymentMethod.Card => "Carte",
        PaymentMethod.BankTransfer => "Virement",
        PaymentMethod.Notchpay => "Paiement en ligne Notch Pay",
        _ => method.ToString()
    };
}

public enum OrderPaymentStatus
{
    Pending,
    Confirmed,
    Failed,
    Refunded
}
