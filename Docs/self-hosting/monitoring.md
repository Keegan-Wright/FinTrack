# Monitoring

FinTrack sends logs, traces and metrics over OpenTelemetry, so you can use any tool that accepts OTLP. Nothing is sent
until you set `OTEL_EXPORTER_OTLP_ENDPOINT`, e.g. `http://otel-lgtm:4317`.

[grafana/otel-lgtm](https://github.com/grafana/docker-otel-lgtm) is an easy place to start, with everything in one
container.

## What's captured

No amounts, descriptions or names are sent, only counts, timings and ids. Logs and traces are tagged with the user's id.

### Logs

Structured, and linked to their traces.

### Traces

- Requests, outgoing calls such as TrueLayer and webhooks, and database queries.
- Background jobs and the live update hub.
- Bank syncs, broken down by connection, account and card, with what each one changed.

### Metrics

FinTrack's own, on the `FinanceTracker` meter.

| Metric | Description |
|---|---|
| `fintrack.sync.connection.duration` | How long each connection takes to sync. |
| `fintrack.sync.transactions` | Transactions added or updated by syncs. |
| `fintrack.sync.fetches.failed` | Failed fetches from a bank. |
| `fintrack.sync.oldest_age` | Time since the stalest connection last synced. |
| `fintrack.connections` | Connections by state, such as ok, renewal due or failing. |
| `fintrack.truelayer.throttle.wait` | Time spent waiting on FinTrack's TrueLayer rate limit. |
| `fintrack.notifications.raised` | Notifications raised. |
| `fintrack.notifications.delivered` | Notifications sent by email or webhook, and whether they arrived. |
| `fintrack.emails.sent` | Emails sent, and whether the mail server accepted them. |
| `fintrack.sso.failures` | Failed single sign-on attempts, and why. |
| `fintrack.rules.transactions_changed` | Transactions changed by rules. |
| `fintrack.transfers.decisions` | Transfers found, sent for review or cleared. |
| `fintrack.bills.changes` | Bills added, ended or changing price. |
| `fintrack.bills.transactions_matched` | Transactions matched to or from bills. |
| `fintrack.subscriptions.suggested` | Subscriptions suggested. |
| `fintrack.balances.snapshots_refreshed` | Daily balance snapshots, used for net worth. |

Plus the standard ones from ASP.NET Core, HttpClient, the .NET runtime, Npgsql, Entity Framework Core, Identity, Polly
and Quartz.

## Profiles

The image includes Pyroscope's .NET profiler for CPU, memory, lock and exception profiles. To turn it on, uncomment
the `CORECLR_*`, `LD_PRELOAD` and `PYROSCOPE_*` lines in the compose file and set `PYROSCOPE_SERVER_ADDRESS`, e.g.
`http://otel-lgtm:4040`.

## Grafana dashboards

There are six dashboards in [Grafana/dashboards](../../Grafana/dashboards).

- **Overview.** Health, response times and syncing at a glance.
- **Bank sync.** Connections, syncs, TrueLayer and the scheduled jobs.
- **Notifications and email.** What was sent, and whether it arrived.
- **Sign-ins and pages.** Sign ins, permissions and page use.
- **Runtime and database.** The process, .NET runtime, database and outgoing calls.
- **Logs and traces.** Search logs and find failed or slow requests.

Import them in Grafana, or mount them with [Grafana/dashboards.yaml](../../Grafana/dashboards.yaml) to load them into
otel-lgtm automatically.
