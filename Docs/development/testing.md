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

## Coverage

```sh
Scripts/coverage.sh              # unit and integration tests
Scripts/coverage.sh --unit-only  # quicker, no Docker needed
```

This runs the tests with [dotnet-coverage](https://learn.microsoft.com/dotnet/core/additional-tools/dotnet-coverage)
and writes an HTML report to `TestResults/Coverage/report`. The integration tests count the app's code as well, as the
app Aspire starts picks up the coverage profiler from the test run. `Test/CodeCoverage.xml` keeps it to FinTrack's own
code, leaving out the tests, the AppHost, migrations and generated code. Code in the browser isn't measured, only the
Client's unit tests and its server-side rendering count.

Every pull request gets the same numbers as a comment, updated on each push, and in the build's summary. They're for
information, a drop doesn't fail the build.

## Writing tests

- Arrange, act and assert, each marked with a comment.
- Set up data with the scenario builder in `FinanceTracker.Tests.Unit.Shared`, e.g.
  `await Scenario.Do(s => s.UserScenario.WithUser(Username))`.
- Use made up names and numbers, never real ones.
- UI tests act like a user, clicking and typing rather than calling code.

## Screenshots

The README's screenshots come from a test with six months of made up data. It only runs when asked for:

```sh
dotnet test --project Test/FinanceTracker.Tests.Integration -- --treenode-filter '/*/*/ScreenshotTests/*'
```

The images are saved to `Docs/images`. Run it again when a page they show changes.
