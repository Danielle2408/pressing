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
