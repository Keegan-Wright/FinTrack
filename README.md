# FinTrack

FinTrack is a self-hosted personal finance app. It syncs your bank accounts and cards through open banking, then helps
you see where your money goes, plan what's coming and keep on top of it, on your own server.

## Highlights

- **Bank sync built in.** Accounts and credit cards from UK banks through TrueLayer, synced on a schedule, with renewal
  reminders before a bank's consent runs out.
- **Transactions you can trust.** Transfers between your own accounts are matched so they aren't counted as spending,
  pending payments are tidied up once they settle, and transactions can be split.
- **Rules.** Tag transactions, mark transfers, or link bills and debts automatically. Preview a rule against your history
  before applying it.
- **Bills and subscriptions.** Standing orders and direct debits become bills, repeat payments are suggested as
  subscriptions, and each period shows paid, late or a changed price.
- **Budgets.** Monthly limits on your tags, with rollover, following your pay day if you like.
- **Savings goals and debts.** Goals that track an account or tagged transactions, and debts with a pay-off forecast.
- **Net worth.** Daily balance history for every account, back-filled from your transactions.
- **Reports and exports.** Spending over time, by account or by tag, with PDF statements, reports and a full backup.
- **Notifications.** In the app, by email or to a webhook, for things like late bills, low balances and large payments.
- **Sharing.** Link with someone in your household to share accounts, tags, rules, bills and more, or merge a joint
  account you've both connected.
- **Private by design.** Sensitive columns are encrypted at rest, sign in can go through your own identity provider,
  and your data stays on your server apart from the calls to your bank, and the email, webhooks and monitoring you set
  up yourself.

## Tech stack

| Area | Built with |
|---|---|
| App | .NET 10, ASP.NET Core, Blazor WebAssembly, MudBlazor, ApexCharts |
| Data | PostgreSQL, Entity Framework Core, AES-GCM column encryption |
| Background jobs | Quartz.NET |
| Open banking | TrueLayer |
| Exports and email | QuestPDF, MailKit |
| Running locally | .NET Aspire, Mailpit |
| Observability | OpenTelemetry |
| Tests | TUnit, bUnit, Playwright, WireMock |

## Getting started

- **Self-hosting:** run the published Docker image with Docker Compose, see
  [Self-hosting](Docs/self-hosting/README.md).
- **Developing:** run everything locally with the Aspire AppHost, see [Development](Docs/development/README.md).

All the guides are listed in [Docs](Docs/README.md).

## Contributing

Contributions are welcome. Please read the contributing guide before opening a pull request. Bugs, ideas and security
issues all go in an issue.

## Licence

MIT, see [LICENSE](LICENSE).
