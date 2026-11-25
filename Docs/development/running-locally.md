# Running locally

FinTrack runs locally through the .NET Aspire AppHost, which starts Postgres, a mail catcher and the app together.

## You'll need

- The .NET 11 SDK.
- Docker, for Postgres and the mail catcher.
- A trusted development certificate, `dotnet dev-certs https --trust`.
- TrueLayer sandbox credentials, see [Your bank connection](../self-hosting/open-banking.md).

## Secrets

Settings go in the AppHost's user secrets.

```sh
cd Src/FinanceTracker.AppHost
dotnet user-secrets set "Encryption:Keys" "1:$(openssl rand -base64 32)"
dotnet user-secrets set "Encryption:CurrentKeyId" "1"
dotnet user-secrets set "OpenBanking:TrueLayer:PublicIpAddress" "<your public IP>"
```

Then point TrueLayer at the sandbox. The script asks for your client id and secret.

```sh
Scripts/truelayer-env.sh --sandbox
```

`--live` switches back to real banks.

Single sign-on is optional. Set `OIDC:Authority`, `OIDC:ClientId` and `OIDC:ClientSecret` to use it, with
`https://localhost:7180/signin-oidc` as the redirect URI.

## Running

From the repo root, with the [Aspire CLI](https://aspire.dev):

```sh
aspire run
```

Or `dotnet run --project Src/FinanceTracker.AppHost`, or run the AppHost from your IDE.

To run with the AI module built in, the way the `fintrackweb-ai` image is, set `FinTrackAi`:

```sh
FinTrackAi=true aspire run
```

MSBuild reads it from the environment, so it works with any of the above.

The Aspire dashboard links to the app, its logs and traces, and Mailpit, where any emails end up.
