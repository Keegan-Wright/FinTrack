# Architecture

FinTrack is a Blazor app. The server renders the sign in pages and hosts the API, and everything else runs in the
browser as WebAssembly, calling that API.

## Projects

| Project | What it holds |
|---|---|
| `FinanceTracker` | The web host. API endpoints, the sign in pages, background jobs and the live update hub. |
| `FinanceTracker.Client` | The pages and components that run in the browser, and the HTTP clients for the API. |
| `FinanceTracker.Client.Common` | The shared building blocks pages are made from like headers, sections, stats, money text, the search field, the page and dialog bases with the loading overlay. |
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
| `Modules/Ai/` | The AI module, `FinanceTracker.Ai` on the server and `FinanceTracker.Ai.Client` in the browser. Only built in with `FinTrackAi=true`, see [Modules](#modules). |

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

## Modules

A module is a feature only some installs want, built into its own image rather than switched off in every one. Each
lives in `Src/Modules/<Name>/` with a matching solution folder. AI is the only one, built in with `FinTrackAi=true` for
the `fintrackweb-ai` image. The plain `fintrackweb` image has none of its code, packages, pages or tables.

- The build switch adds the module's projects and defines `FINTRACK_AI`. The few places that wire it in sit behind
  `#if FINTRACK_AI`, the client's `Ai/AiModuleLoader.cs`, `Routes.razor.cs` and `LayoutBase.cs`, and the server's
  `Program.cs`, `Extensions/AiModuleHostExtensions.cs` and `Extensions/RoutedAssemblies.cs`.
- The module's browser assembly is lazy loaded, so the browser only fetches it when it's needed. The app can't refer to
  its types directly, the layout renders its app bar button by name.
- Its tables live in their own schema, `ai`, with their own migrations history, so moving back to the plain image
  leaves them alone. Its migrations are made from the module's project on its own:

  ```sh
  dotnet ef migrations add <Name> --project Src/Modules/Ai/FinanceTracker.Ai \
    --startup-project Src/Modules/Ai/FinanceTracker.Ai --output-dir Persistence/Migrations
  ```

## Data

- Postgres through Entity Framework Core. Migrations run when the app starts.
- Sensitive columns are encrypted, see [Encryption keys](../self-hosting/encryption-keys.md).
- Each user only sees their own data, plus what's been shared with them.

## Background jobs

Quartz runs the scheduled work, such as bank syncs, notifications and daily balance snapshots. Jobs are stored in
Postgres, and admins can see and run them from the Automation page.
