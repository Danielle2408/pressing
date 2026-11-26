using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pressing.Data;
using Pressing.Models;
using Pressing.Security;
using Pressing.ViewModels;
using Pressing.Services;

namespace Pressing.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.Operator)]
[Route("Comptoir/Commandes/[action]")]
public sealed class CommandesController(
    ApplicationDbContext database,
    IOptions<OrderSettings> orderOptions) : Controller
{
    private OrderSettings Settings => orderOptions.Value;

    [HttpGet]
    public async Task<IActionResult> Index(OrderStatus? status, string? query)
    {
        var orders = database.LaundryOrders.AsNoTracking()
            .Include(order => order.Customer)
            .AsQueryable();

        if (status.HasValue)
            orders = orders.Where(order => order.Status == status.Value);

        var trimmed = query?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            var phone = NormalizePhone(trimmed);
            var namePart = trimmed.ToLowerInvariant();
            orders = string.IsNullOrEmpty(phone)
                ? orders.Where(order => order.Number.Contains(trimmed) ||
                                        order.Customer.FullName.ToLower().Contains(namePart))
                : orders.Where(order => order.Number.Contains(trimmed) ||
                                        order.Customer.FullName.ToLower().Contains(namePart) ||
                                        order.Customer.NormalizedPhone.Contains(phone));
        }

        var result = await orders.OrderByDescending(order => order.CreatedAtUtc).Take(100).ToListAsync();
        return View(new OrderListViewModel { Status = status, Query = trimmed, Orders = result });
    }

    [HttpGet]
    public async Task<IActionResult> Creer()
    {
        var form = new CreateOrderViewModel
        {
            ExpectedReadyAtLocal = DateTime.Now.AddDays(Math.Max(1, Settings.DefaultProcessingDays))
        };
        return View(await BuildCreatePageAsync(form));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Creer(OrderCreatePageViewModel page)
    {
        var model = page.Form;
        if (model.Lines.Count is < 1 or > 30)
            ModelState.AddModelError("Form.Lines", "Une commande doit contenir de 1 à 30 lignes d’articles.");

        if (model.ExpectedReadyAtLocal <= DateTime.Now)
            ModelState.AddModelError("Form.ExpectedReadyAtLocal", "La date prévue doit être dans le futur.");

        var customer = await database.Customers.FirstOrDefaultAsync(item => item.Id == model.CustomerId);
        if (customer is null)
            ModelState.AddModelError("Form.CustomerId", "Le client sélectionné n’existe pas.");
        else if ((model.HomeCollection || model.HomeDelivery) && string.IsNullOrWhiteSpace(customer.Address))
            ModelState.AddModelError("Form.CustomerId", "Renseigne l’adresse du client avant une collecte ou livraison à domicile.");

        if (!ModelState.IsValid || customer is null)
            return View(await BuildCreatePageAsync(model));

        var now = DateTime.UtcNow;
        var orderLines = new List<OrderLine>();
        var baseAmount = 0m;
        foreach (var input in model.Lines)
        {
            var tariff = await database.ServiceTariffs
                .Include(item => item.GarmentCategory)
                .Include(item => item.LaundryService)
                .Where(item => item.GarmentCategoryId == input.CategoryId &&
                               item.LaundryServiceId == input.ServiceId &&
                               item.GarmentCategory.IsActive && item.LaundryService.IsActive &&
                               item.ValidFromUtc <= now &&
                               (item.ValidUntilUtc == null || item.ValidUntilUtc > now))
                .OrderByDescending(item => item.ValidFromUtc)
                .FirstOrDefaultAsync();

            if (tariff is null)
            {
                ModelState.AddModelError("Form.Lines", "Un des articles sélectionnés ne possède plus de tarif actif. Vérifie le catalogue.");
                return View(await BuildCreatePageAsync(model));
            }

            var lineTotal = decimal.Round(tariff.Price * input.Quantity, 2);
            baseAmount += lineTotal;
            orderLines.Add(new OrderLine
            {
                ServiceTariffId = tariff.Id,
                CategoryName = tariff.GarmentCategory.Name,
                ServiceName = tariff.LaundryService.Name,
                Quantity = input.Quantity,
                UnitPrice = tariff.Price,
                LineTotal = lineTotal,
                Color = CleanOptional(input.Color),
                Brand = CleanOptional(input.Brand),
                ReceptionCondition = CleanOptional(input.ReceptionCondition),
                Notes = CleanOptional(input.Notes)
            });
        }

        var expressSurcharge = model.Express
            ? decimal.Round(baseAmount * Settings.ExpressSurchargePercent / 100m, 2)
            : 0m;
        var collectionFee = model.HomeCollection ? Settings.HomeCollectionFee : 0m;
        var deliveryFee = model.HomeDelivery ? Settings.HomeDeliveryFee : 0m;
        var expectedReadyAt = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(model.ExpectedReadyAtLocal, DateTimeKind.Unspecified), TimeZoneInfo.Local);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        await using var transaction = await database.Database.BeginTransactionAsync();
        var order = new LaundryOrder
        {
            Number = $"TEMP-{Guid.NewGuid():N}",
            CustomerId = customer.Id,
            CreatedByUserId = userId,
            Status = OrderStatus.Received,
            CreatedAtUtc = now,
            ExpectedReadyAtUtc = expectedReadyAt,
            Express = model.Express,
            HomeCollection = model.HomeCollection,
            HomeDelivery = model.HomeDelivery,
            Notes = CleanOptional(model.Notes),
            ExpressSurcharge = expressSurcharge,
            CollectionFee = collectionFee,
            DeliveryFee = deliveryFee,
            TotalAmount = baseAmount + expressSurcharge + collectionFee + deliveryFee,
            Lines = orderLines
        };
        order.StatusHistory.Add(new OrderStatusHistory
        {
            PreviousStatus = null,
            NewStatus = OrderStatus.Received,
            UserId = userId,
            ChangedAtUtc = now
        });

        database.LaundryOrders.Add(order);
        await database.SaveChangesAsync();
        order.Number = $"PR-{now.Year}-{order.Id:D6}";
        await database.SaveChangesAsync();
        await transaction.CommitAsync();

        if (!string.IsNullOrWhiteSpace(customer.ApplicationUserId))
        {
            database.UserNotifications.Add(new UserNotification
            {
                UserId = customer.ApplicationUserId, Title = "Commande reçue",
                Message = $"La commande {order.Number} a été enregistrée par le pressing.",
                Url = $"/EspaceClient/Details/{order.Id}", CreatedAtUtc = now
            });
            await database.SaveChangesAsync();
        }

        TempData["OrderSuccess"] = $"La commande {order.Number} a été créée.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await database.LaundryOrders.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.CreatedByUser)
            .Include(item => item.Lines)
            .Include(item => item.Payments).ThenInclude(item => item.CreatedByUser)
            .Include(item => item.StatusHistory).ThenInclude(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (order is null)
            return NotFound();

        var allowedStatuses = GetAllowedStatuses(order.Status)
            .Where(status => status != OrderStatus.Cancelled || User.IsInRole(Roles.Administrator))
            .ToList();
        return View(new OrderDetailsViewModel { Order = order, AllowedStatuses = allowedStatuses });
    }

    [HttpGet]
    public async Task<IActionResult> Recu(int paymentId)
    {
        var payment = await database.OrderPayments.AsNoTracking()
            .Include(item => item.LaundryOrder).ThenInclude(order => order.Customer)
            .Include(item => item.LaundryOrder).ThenInclude(order => order.Lines)
            .FirstOrDefaultAsync(item => item.Id == paymentId && item.Status == OrderPaymentStatus.Confirmed && item.ReceiptNumber != null);
        if (payment is null) return NotFound();
        var fileName = ReceiptPdfGenerator.InvalidFileNameCharacters().Replace(payment.ReceiptNumber!, "_") + ".pdf";
        return File(ReceiptPdfGenerator.Generate(payment), "application/pdf", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangerStatut(int id, OrderStatus status, string? reason)
    {
        var order = await database.LaundryOrders.FirstOrDefaultAsync(item => item.Id == id);
        if (order is null)
            return NotFound();

        var isAdmin = User.IsInRole(Roles.Administrator);
        if (!GetAllowedStatuses(order.Status).Contains(status) ||
            (status == OrderStatus.Cancelled && !isAdmin))
        {
            return Forbid();
        }

        var cleanReason = CleanOptional(reason);
        if (status == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(cleanReason))
        {
            TempData["OrderError"] = "Un motif est obligatoire pour annuler une commande.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (status == OrderStatus.Delivered && order.AmountPaid < order.TotalAmount)
        {
            TempData["OrderError"] = "Encaisse le solde de la commande avant de confirmer le retrait.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var previousStatus = order.Status;
        order.Status = status;
        if (status == OrderStatus.Cancelled)
            order.CancellationReason = cleanReason;

        database.OrderStatusHistories.Add(new OrderStatusHistory
        {
            LaundryOrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = status,
            UserId = userId,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = cleanReason
        });
        var customerUserId = await database.Customers.Where(item => item.Id == order.CustomerId)
            .Select(item => item.ApplicationUserId).FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(customerUserId))
        {
            database.UserNotifications.Add(new UserNotification
            {
                UserId = customerUserId, Title = "Suivi de commande mis à jour",
                Message = $"La commande {order.Number} est maintenant « {status.ToFrench()} ».",
                Url = $"/EspaceClient/Details/{order.Id}", CreatedAtUtc = DateTime.UtcNow
            });
        }
        await database.SaveChangesAsync();

        TempData["OrderSuccess"] = $"La commande est maintenant au statut « {status.ToFrench()} ».";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnregistrerPaiement(
        int id,
        decimal amount,
        PaymentMethod method,
        string? reference)
    {
        if (!Enum.IsDefined(method) || method == PaymentMethod.Notchpay)
            return BadRequest("Choisis un moyen de paiement reçu au comptoir.");
        if (amount <= 0)
        {
            TempData["OrderError"] = "Le montant encaissé doit être supérieur à 0 FCFA.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var transactionReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (transactionReference?.Length > 120)
        {
            TempData["OrderError"] = "La référence du paiement ne peut pas dépasser 120 caractères.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        await using var transaction = await database.Database.BeginTransactionAsync();
        var order = await database.LaundryOrders
            .FromSqlInterpolated($"SELECT * FROM \"LaundryOrders\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (order is null)
            return NotFound();
        if (order.Status == OrderStatus.Cancelled)
        {
            TempData["OrderError"] = "Une commande annulée ne peut pas recevoir de paiement.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (await database.OrderPayments.AnyAsync(payment => payment.LaundryOrderId == id &&
                payment.Method == PaymentMethod.Notchpay && payment.Status == OrderPaymentStatus.Pending))
        {
            TempData["OrderError"] = "Un paiement Notch Pay est en attente. Vérifie son résultat avant un nouvel encaissement.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var remaining = order.TotalAmount - order.AmountPaid;
        if (amount > remaining)
        {
            TempData["OrderError"] = $"Le montant dépasse le solde dû de {remaining:N0} FCFA.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (transactionReference is not null && await database.OrderPayments.AnyAsync(payment =>
                payment.Method == method && payment.TransactionReference == transactionReference))
        {
            TempData["OrderError"] = "Cette référence de transaction a déjà été enregistrée.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = DateTime.UtcNow;
        var payment = new OrderPayment
        {
            ReceiptNumber = $"TEMP-{Guid.NewGuid():N}",
            MerchantReference = $"counter-{Guid.NewGuid():N}",
            LaundryOrderId = id,
            Amount = decimal.Round(amount, 2),
            Method = method,
            Status = OrderPaymentStatus.Confirmed,
            Currency = "XAF",
            TransactionReference = transactionReference,
            CreatedByUserId = userId,
            CreatedAtUtc = now
        };
        order.AmountPaid += payment.Amount;
        database.OrderPayments.Add(payment);
        var customerUserId = await database.Customers.Where(item => item.Id == order.CustomerId)
            .Select(item => item.ApplicationUserId).FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(customerUserId))
        {
            database.UserNotifications.Add(new UserNotification
            {
                UserId = customerUserId, Title = "Paiement enregistré",
                Message = $"Un paiement de {payment.Amount:N0} FCFA a été enregistré pour {order.Number}.",
                Url = $"/EspaceClient/Details/{order.Id}", CreatedAtUtc = now
            });
        }
        await database.SaveChangesAsync();
        payment.ReceiptNumber = $"REC-{now.Year}-{payment.Id:D6}";
        await database.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["OrderSuccess"] = $"Paiement de {payment.Amount:N0} FCFA enregistré. Reçu {payment.ReceiptNumber}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<OrderCreatePageViewModel> BuildCreatePageAsync(CreateOrderViewModel form)
    {
        var now = DateTime.UtcNow;
        var prices = await database.ServiceTariffs.AsNoTracking()
            .Where(item => item.GarmentCategory.IsActive && item.LaundryService.IsActive &&
                           item.ValidFromUtc <= now && (item.ValidUntilUtc == null || item.ValidUntilUtc > now))
            .Select(item => new OrderPriceOptionViewModel
            {
                CategoryId = item.GarmentCategoryId,
                ServiceId = item.LaundryServiceId,
                ServiceName = item.LaundryService.Name,
                Price = item.Price
            })
            .ToListAsync();

        return new OrderCreatePageViewModel
        {
            Form = form,
            Customers = await database.Customers.AsNoTracking().OrderBy(item => item.FullName).ToListAsync(),
            Categories = await database.GarmentCategories.AsNoTracking().Where(item => item.IsActive)
                .OrderBy(item => item.Name).ToListAsync(),
            Services = await database.LaundryServices.AsNoTracking().Where(item => item.IsActive)
                .OrderBy(item => item.Name).ToListAsync(),
            Prices = prices,
            Settings = Settings
        };
    }

    private static IReadOnlyList<OrderStatus> GetAllowedStatuses(OrderStatus current) => current switch
    {
        OrderStatus.PendingConfirmation => [OrderStatus.Received, OrderStatus.Cancelled],
        OrderStatus.Received => [OrderStatus.Washing, OrderStatus.DryCleaning, OrderStatus.Cancelled],
        OrderStatus.Washing or OrderStatus.DryCleaning => [OrderStatus.Ironing, OrderStatus.Ready, OrderStatus.Cancelled],
        OrderStatus.Ironing => [OrderStatus.Ready, OrderStatus.Cancelled],
        OrderStatus.Ready => [OrderStatus.Delivered],
        _ => []
    };

    private static string NormalizePhone(string value) => Regex.Replace(value, @"[^0-9+]", string.Empty);

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
