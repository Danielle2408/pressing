# Pressing

Application de gestion de pressing construite avec ASP.NET Core MVC, ASP.NET Core Identity et PostgreSQL.

## Prérequis

- .NET 10 SDK
- Docker Desktop avec Compose, ou PostgreSQL 15 ou supérieur

## Démarrage local avec Docker

1. Copier `.env.example` vers `.env` et choisir un mot de passe local pour PostgreSQL.
2. Démarrer la base : `docker compose up -d db`.
3. Définir le mot de passe administrateur initial et lancer l’application :

```powershell
$env:InitialAdmin__Email = "admin@example.com"
$env:InitialAdmin__Password = "UnMotDePasseLongEtUnique!"
dotnet run
```

Au démarrage, l’application applique les migrations PostgreSQL puis crée les rôles `Administrateur`, `Operateur` et `Client`. Le compte administrateur initial n’est créé que si `InitialAdmin__Email` et `InitialAdmin__Password` sont tous deux définis. Garde le mot de passe réel uniquement dans l’environnement local ou dans le gestionnaire de secrets de l’hébergeur.

Pour une base PostgreSQL administrée séparément, définir `ConnectionStrings__Pressing` avant `dotnet run`. La valeur de développement de `appsettings.json` ne doit pas être utilisée en production.

## Paiement en ligne Notch Pay

Le paiement en ligne utilise l’API et la page hébergée Notch Pay. Il n’est confirmé qu’après vérification serveur de la transaction. Configure les variables d’environnement dans l’hébergement (ne mets jamais les clés dans Git) :

- `NotchPay__PublicKey` : clé publique de l’environnement Notch Pay choisi (test ou production).
- `NotchPay__WebhookHashKey` : clé de signature associée au webhook configuré dans Notch Pay.
- `NotchPay__CallbackBaseUrl` : adresse HTTPS publique de l’application, sans slash final.

Dans le tableau de bord Notch Pay, configure le webhook sur `https://<domaine-public>/webhooks/notchpay`. La route de retour client est générée par l’application. Une URL locale non accessible depuis Internet ne permet pas de recevoir les notifications en production. Commence avec les clés de test Notch Pay, puis passe aux clés de production seulement lorsque le compte marchand et le déploiement sont prêts.

## Fidélité

La règle d’attribution est configurée par `Loyalty__CurrencyPerPoint` (1 000 FCFA par défaut). Les points sont crédités une seule fois quand le solde d’une commande est intégralement payé. Le solde et l’historique sont consultables dans l’espace client.

## Migrations

Les migrations EF Core sont versionnées dans `Data/Migrations` et appliquées au démarrage.

Pour en créer une après modification du modèle :

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add NomDeLaMigration
```

## Rôles

- `Administrateur`
- `Operateur`
- `Client`

L’inscription publique crée uniquement des comptes Client. Les comptes du personnel seront gérés depuis l’interface administrateur.
