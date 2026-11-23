# Migrations

After changing the model, add a migration:

```sh
dotnet ef migrations add <Name>
```

Check nothing's been missed:

```sh
dotnet ef migrations has-pending-model-changes
```

`.config/dotnet-ef.json` points `dotnet ef` at the migrations and startup projects, so these work from anywhere in the
repo. Options given on the command line win over it.

Migrations run when the app starts, so there's nothing else to do. Read the generated migration before committing it,
especially anything that drops or renames a column.

To take back the last migration before it's committed, without a database running:

```sh
dotnet ef migrations remove --offline
```

The EF tools are in the repo's tool manifest, at the same version as EF. Run `dotnet tool restore` if you don't have
them yet.
