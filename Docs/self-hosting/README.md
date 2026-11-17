# Self-hosting

FinTrack runs as one Docker image, `keeganwright12/fintrackweb`, with a Postgres database beside it. Docker Compose runs
both.

> [!WARNING]
> Back up your encryption keys. FinTrack encrypts sensitive data with them, and without them it can't be read, not
> even from a database backup.

## Guides

These are being written. Each will be linked here as it's added.

- **Installing:** the compose file, the database, and the first start.
- **Configuration:** every setting, in one table.
- **Your bank connection:** a TrueLayer app, sandbox or live, and its redirect address.
- **Encryption keys:** backing them up and rotating them.
- **Email:** the mail server settings, with examples, and what works without email.
- **Single sign-on:** signing in through an identity provider, with Authelia as the example.
- **Reverse proxy:** HTTPS, forwarded headers and trusted proxies.
- **Backups and upgrades:** what to back up, and how an upgrade migrates the database.
- **Monitoring:** sending logs, traces, metrics and profiles to your own tools, with Grafana as the example.
