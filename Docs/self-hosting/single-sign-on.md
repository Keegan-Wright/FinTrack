# Single sign-on

FinTrack can sign people in through an OpenID Connect provider, such as Authelia, Authentik or Keycloak. The sign in
page then has a single sign on button alongside email and password.

## Set up the client

Create a client for FinTrack at your provider:

- **Redirect URI** `https://fintrack.example.com/signin-oidc`, using your address.
- **Scopes** `openid`, `profile` and `email`.
- **Flow** authorization code, with PKCE (`S256`).
- **Client authentication** `client_secret_post`.

Then set `OIDC_AUTHORITY`, `OIDC_CLIENT_ID` and `OIDC_CLIENT_SECRET`. Single sign-on turns on once all three are set.
The provider needs to be on HTTPS.

## Authelia example

```yaml
identity_providers:
  oidc:
    clients:
      - client_id: fintrack
        client_name: FinTrack
        client_secret: '<hashed secret>'
        public: false
        authorization_policy: two_factor
        consent_mode: implicit
        require_pkce: true
        pkce_challenge_method: S256
        redirect_uris:
          - 'https://fintrack.example.com/signin-oidc'
        scopes: [openid, profile, email]
        response_types: [code]
        grant_types: [authorization_code]
        token_endpoint_auth_method: client_secret_post
```

```sh
OIDC_AUTHORITY=https://auth.example.com
OIDC_CLIENT_ID=fintrack
OIDC_CLIENT_SECRET=<the secret, before hashing>
```

## Accounts

The first time someone signs in this way, FinTrack links them to the user with the same email, or creates a new user.
Their email has to be verified by the provider.

## Troubleshooting

If it fails you're sent back to the sign in page, and the logs say why. It's usually the redirect URI, the client
authentication method, or an unverified email.
