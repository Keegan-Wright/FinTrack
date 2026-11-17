# Contributing

Thanks for helping out. This covers how changes are made and what's expected of a pull request.

## Before you start

Open an issue first, or comment on an existing one, so we can agree on the change before you spend time on it. Small
fixes such as typos don't need one.

## Getting set up

See [Running locally](Docs/development/running-locally.md) and [Architecture](Docs/development/architecture.md).

## Branches and pull requests

- Branch from `main`, named after the issue, e.g. `issue/42-budget-rollover`.
- Keep a pull request to one change. Large features can land over several.
- Fill in the pull request template, and link the issue.
- The build and every test must pass. Warnings fail the build.

## Code

The build enforces most of the style through `.editorconfig`. Beyond that, a few conventions.

- **One type per file.**
- **Doc comments on every member,** private methods included. Keep them short and plain. The build fails when a
  public one is missing, tests aside.
- **Readable over clever.** No nested ternaries or dense one-liners, use ifs, early returns and well named helpers.
- **Full, descriptive names,** e.g. `_accountSnapshotFetcher` rather than `_fetcher`.
- **C# 14 extension blocks** for extension methods, rather than `this` parameters.
- **Register services with the attributes,** against an interface, see
  [Architecture](Docs/development/architecture.md#registering-services).
- **Use built-in features** of .NET and the libraries before writing your own.
- **No hard-coded culture.** Dates and money use the configured culture.

## UI

- **Works on every screen size,** phone, tablet and desktop.
- **Component styles in the component's `.razor.css`.** `App.css` is only for shared styles.
- **Theme and money colours come from `FinanceTracker.Design`,** never copied.
- **Don't reload the page for the user.** Show a message and let them reload.

## Tests

- New behaviour comes with tests, and bug fixes with a test that would have caught the bug. See
  [Testing](Docs/development/testing.md).
- Use made up data. Never real names, account numbers or other personal details, anywhere in the repo.

## Docs

If a change affects setup, a setting or how something works, update [Docs](Docs) in the same pull request. A new
setting has to be added to the [configuration reference](Docs/self-hosting/configuration.md), and a test fails until
it is.

## Issues

Raise bugs, ideas and security issues as GitHub issues.
