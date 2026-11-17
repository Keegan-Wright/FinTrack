# Architecture

FinTrack is a Blazor app. The server renders the sign in pages and hosts the API, and everything else runs in the
browser as WebAssembly, calling that API.

## Projects

| Project | What it holds |
|---|---|
| `FinanceTracker` | The web host. API endpoints, the sign in pages, background jobs and the live update hub. |
| `FinanceTracker.Client` | The pages and components that run in the browser, and the HTTP clients for the API. |
| `FinanceTracker.Contracts` | What the server and browser share. Requests, responses, routes and service interfaces. |
| `FinanceTracker.Domain` | The entities and their rules, with no dependencies on the database or web. |
| `FinanceTracker.Infrastructure` | The services behind the API, the database context, TrueLayer, exports, notifications and encryption. |
| `FinanceTracker.Infrastructure.Migrations` | The database migrations. |
| `FinanceTracker.Emails` | Email templates, rendered from Razor components. |
| `FinanceTracker.Design` | The theme and money colours, used by both the app and emails. |
| `FinanceTracker.Telemetry` | Metrics, trace sources and log tags. |
| `FinanceTracker.AppHost` | Runs everything locally, see [Running locally](running-locally.md). |
| `FinanceTracker.AppHost.ServiceDefaults` | Shared setup such as telemetry and health checks, and the app's setting names. |
| `Generators/` | Source generators for registering services. |

## Registering services

Services aren't registered by hand. Mark a class with `[Scoped<T>]`, `[Singleton<T>]` or `[Transient<T>]` and
`[InjectionCategory]`, and a source generator registers it against its interface on the server, in the browser, or
both.

The same interface often has one of each. The server's does the work:

```csharp
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IBillsService>]
public class BillsService : ServiceBase<BillsService>, IBillsService
```

And the browser's calls the API:

```csharp
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IBillsService>]
public sealed class BillsApiClient : IBillsService
```

Validators are shared by both:

```csharp
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<SplitTransaction>>]
public class SplitTransactionValidator : AbstractValidator<SplitTransaction>
```

Each app then calls the generated `AddFinanceTrackerServices` and `AddFinanceTrackerValidators` in its `Program.cs`.

## Data

- Postgres through Entity Framework Core. Migrations run when the app starts.
- Sensitive columns are encrypted, see [Encryption keys](../self-hosting/encryption-keys.md).
- Each user only sees their own data, plus what's been shared with them.

## Background jobs

Quartz runs the scheduled work, such as bank syncs, notifications and daily balance snapshots. Jobs are stored in
Postgres, and admins can see and run them from the Automation page.
