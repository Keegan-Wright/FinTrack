# Migrations

After changing the model, add a migration from the repo root.

```sh
dotnet ef migrations add <Name> --project Src/FinanceTracker.Infrastructure.Migrations --startup-project Src/FinanceTracker
```

Check nothing's been missed:

```sh
dotnet ef migrations has-pending-model-changes --project Src/FinanceTracker.Infrastructure.Migrations --startup-project Src/FinanceTracker
```

Migrations run when the app starts, so there's nothing else to do. Read the generated migration before committing it,
especially anything that drops or renames a column.

If you don't have the EF tools yet, `dotnet tool install --global dotnet-ef`.
