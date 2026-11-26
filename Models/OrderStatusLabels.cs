namespace Pressing.Models;

public static class OrderStatusLabels
{
    public static string ToFrench(this OrderStatus status) => status switch
    {
        OrderStatus.PendingConfirmation => "À confirmer",
        OrderStatus.Received => "Reçue",
        OrderStatus.Washing => "En lavage",
        OrderStatus.DryCleaning => "En nettoyage à sec",
        OrderStatus.Ironing => "En repassage",
        OrderStatus.Ready => "Prête",
        OrderStatus.Delivered => "Livrée",
        OrderStatus.Cancelled => "Annulée",
        _ => status.ToString()
    };
}
