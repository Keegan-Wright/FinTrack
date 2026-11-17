# Testing

Tests use [TUnit](https://tunit.dev), with bUnit for components and Playwright for the browser.

## Unit tests

```sh
dotnet test --project Test/FinanceTracker.Tests.Unit.Domain
```

There's one project per area: `Domain`, `Infrastructure`, `FinanceTracker` (the web host) and `Client`. They need
nothing running, the database tests use SQLite in memory.

## Integration tests

```sh
dotnet test --project Test/FinanceTracker.Tests.Integration
```

These start the whole app through Aspire, so they need Docker and a trusted development certificate. TrueLayer, email
and single sign-on are faked. The UI tests drive the app in Chromium, which Playwright installs on first run.

Run part of them with a filter, for example only the UI tests:

```sh
dotnet test --project Test/FinanceTracker.Tests.Integration -- --treenode-filter '/*/FinanceTracker.Tests.Integration.Ui/*/*'
```

A failed UI test saves a Playwright trace, which you can open at [trace.playwright.dev](https://trace.playwright.dev).

## Writing tests

- Arrange, act and assert, each marked with a comment.
- Set up data with the scenario builder in `FinanceTracker.Tests.Unit.Shared`, e.g.
  `await Scenario.Do(s => s.UserScenario.WithUser(Username))`.
- Use made up names and numbers, never real ones.
- UI tests act like a user, clicking and typing rather than calling code.
