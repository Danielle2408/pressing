# Pressing

Application web de gestion de pressing construite avec ASP.NET Core MVC, ASP.NET Core Identity et PostgreSQL.

## Prérequis

- .NET 10 SDK
- PostgreSQL 15 ou supérieur

## Configuration

Définir la variable `ConnectionStrings__Pressing` avec la chaîne de connexion PostgreSQL. Ne pas enregistrer de mot de passe réel dans le dépôt.

Exemple local :

```powershell
$env:ConnectionStrings__Pressing = "Host=localhost;Port=5432;Database=pressing;Username=pressing;Password=mot-de-passe-local"
dotnet run
```

Les migrations EF Core seront ajoutées avec le premier module métier.

## Rôles

- `Administrateur`
- `Operateur`
- `Client`

L’inscription publique et la création des comptes du personnel seront définies dans les modules d’authentification.
