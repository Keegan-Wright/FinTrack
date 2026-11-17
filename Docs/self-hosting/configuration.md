# Configuration

Everything goes in the `.env` file next to the compose file. Some settings reach the app under a different name,
shown after "as".

## General

| Setting | Required | Description |
|---|---|---|
| `FINTRACK_VERSION` | Yes | The release to run, a tag from [Docker Hub](https://hub.docker.com/r/keeganwright12/fintrackweb/tags). |
| `FINTRACK_POSTGRES_PASSWORD` | Yes | The database password. |
| `APP_CULTURE` | No | How dates and money are shown, e.g. `en-GB`. |
| `TZ` | No | The time zone, e.g. `Europe/London`. |
| `FINTRACK_PUBLIC_URL` | No | Your FinTrack address, e.g. `https://fintrack.example.com`, for links in emails and webhooks. |

## Encryption

See [Encryption keys](encryption-keys.md).

| Setting | Required | Description |
|---|---|---|
| `FINTRACK_ENCRYPTION_KEYS`, as `ENCRYPTION_KEYS` | Yes | Your keys, as `id:key` pairs separated by `;`. |
| `FINTRACK_ENCRYPTION_CURRENT_KEY_ID`, as `ENCRYPTION_CURRENT_KEY_ID` | Yes | The key new values are encrypted with. |

## Bank connection

See [Your bank connection](open-banking.md).

| Setting | Required | Description |
|---|---|---|
| `OPENBANKING_AUTH_URL`, as `OPEN_BANKING_TRUELAYER_BASE_AUTH_URL` | Yes | TrueLayer's sign in address. |
| `OPENBANKING_DATA_URL`, as `OPEN_BANKING_TRUELAYER_BASE_DATA_URL` | Yes | TrueLayer's data API address. |
| `OPENBANKING_AUTH_REDIRECT_URL`, as `OPEN_BANKING_TRUELAYER_AUTH_REDIRECT_URL` | Yes | Where TrueLayer sends you after connecting a bank. |
| `OPENBANKING_CLIENT_ID`, as `OPEN_BANKING_TRUELAYER_CLIENT_ID` | Yes | Your TrueLayer client id. |
| `OPENBANKING_CLIENT_SECRET`, as `OPEN_BANKING_TRUELAYER_CLIENT_SECRET` | Yes | Your TrueLayer client secret. |
| `OPENBANKING_PUBLIC_IP_ADDRESS`, as `OPEN_BANKING_PUBLIC_IP_ADDRESS` | Yes | Your public IP address. |

## Email

See [Email](email.md).

| Setting | Required | Description |
|---|---|---|
| `SMTP_HOST` | For email | The mail server. Email is off without it. |
| `SMTP_FROM` | For email | The address emails come from. |
| `SMTP_PORT` | No | Defaults to 587. |
| `SMTP_SECURITY` | No | `Auto` (default), `StartTls`, `SslOnConnect` or `None`. |
| `SMTP_USERNAME` | No | The mail server login. |
| `SMTP_PASSWORD` | No | The mail server password. |

## Single sign-on

On when all three are set. See [Single sign-on](single-sign-on.md).

| Setting | Required | Description |
|---|---|---|
| `OIDC_AUTHORITY`, as `OIDC_Authority` | For SSO | Your provider's address. |
| `OIDC_CLIENT_ID`, as `OIDC_ClientId` | For SSO | FinTrack's client id. |
| `OIDC_CLIENT_SECRET`, as `OIDC_ClientSecret` | For SSO | FinTrack's client secret. |

## Network and limits

See [Reverse proxy](reverse-proxy.md).

| Setting | Required | Description |
|---|---|---|
| `FINTRACK_PROXY_NETWORK` | No | Your reverse proxy's Docker network. Defaults to `proxy-net`. |
| `TRUSTED_PROXIES` | Behind a proxy | Your proxy's addresses or networks, comma separated, e.g. `172.18.0.0/16`. |
| `WEBHOOK_ALLOW_PRIVATE` | No | `true` lets webhooks reach your local network, e.g. Home Assistant. Defaults to `false`. |
| `RATE_LIMIT_SIGN_INS_PER_MINUTE` | No | Sign in, register and password reset attempts per minute from one address. Defaults to 10. |
| `RATE_LIMIT_INVITES_PER_HOUR` | No | Sharing invites per hour from one user. Defaults to 20. |

## Monitoring

See [Monitoring](monitoring.md).

| Setting | Required | Description |
|---|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | No | Where to send logs, traces and metrics, e.g. `http://otel-lgtm:4317`. |
| `PYROSCOPE_SERVER_ADDRESS` | No | Where to send profiles, once profiling is turned on. |
