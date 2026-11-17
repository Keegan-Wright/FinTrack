# Your bank connection

FinTrack connects to your banks through [TrueLayer](https://truelayer.com). You need your own TrueLayer credentials
for this.

## Create an app

1. Sign up to the [TrueLayer console](https://console.truelayer.com) and create an app.
2. Put its client id and secret in `OPENBANKING_CLIENT_ID` and `OPENBANKING_CLIENT_SECRET`.
3. Choose a redirect address, add it to the app's allowed redirect URIs, and put it in `OPENBANKING_AUTH_REDIRECT_URL`.
   TrueLayer's own `https://console.truelayer.com/redirect-page` is the simplest, it shows the access code for you to
   copy. Your own address works too, the code arrives as `code` in the URL.
4. Put your public IP address in `OPENBANKING_PUBLIC_IP_ADDRESS`.

## Sandbox or live

The sandbox has test banks with made up data, handy for trying things out. Each has its own credentials.

| | `OPENBANKING_AUTH_URL` | `OPENBANKING_DATA_URL` |
|---|---|---|
| Sandbox | `https://auth.truelayer-sandbox.com/` | `https://api.truelayer-sandbox.com/data/` |
| Live | `https://auth.truelayer.com/` | `https://api.truelayer.com/data/` |

## Connecting a bank

On the Bank connections page, pick your banks and sign in to them. Once you've approved access, copy the access code
from the redirect page and paste it back into FinTrack.

Your accounts then sync on a schedule.

## Renewing

Each bank sets how long its consent lasts. FinTrack reminds you 14 days before it runs out, and renewing is the same
sign in and access code.
