# Email

Email is optional. Without it, notifications still show in the app and can go to webhooks.

Set `SMTP_HOST` and `SMTP_FROM` to turn it on, plus whatever else your mail server needs, see
[Configuration](configuration.md#email). Set `FINTRACK_PUBLIC_URL` too, so emails can link back to FinTrack.

## Gmail

You'll need an [app password](https://myaccount.google.com/apppasswords).

```sh
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_SECURITY=StartTls
SMTP_USERNAME=you@gmail.com
SMTP_PASSWORD=<app password>
SMTP_FROM=you@gmail.com
```

## Other providers

Services like Brevo, Mailgun or Amazon SES give you SMTP details to use the same way.

```sh
SMTP_HOST=smtp.example-provider.com
SMTP_PORT=587
SMTP_USERNAME=<from the provider>
SMTP_PASSWORD=<from the provider>
SMTP_FROM=fintrack@example.com
```

## Testing it

Send yourself a test email from your notification settings. If it doesn't arrive, the logs will say why.
