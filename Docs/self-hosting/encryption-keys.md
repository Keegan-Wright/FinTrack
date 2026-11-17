# Encryption keys

FinTrack encrypts most of what it stores, such as amounts, descriptions, names, account details and bank tokens.

- Values are encrypted with AES-256-GCM, with a random nonce per value.
- Encrypted values that need searching, such as a transaction's id at the bank, also get a blind index, an
  HMAC-SHA256 of the value.
- The encryption and blind index keys are derived from your key with HKDF, so they're never the same.

The keys live in your `.env` file, not the database.

> [!WARNING]
> Back up your keys somewhere away from the server, such as a password manager. Without them your data can't be read,
> even from a database backup.

## Making a key

```sh
openssl rand -base64 32
```

## The keyring

`FINTRACK_ENCRYPTION_KEYS` holds your keys as `id:key` pairs, separated by `;`. `FINTRACK_ENCRYPTION_CURRENT_KEY_ID`
is the one new values are encrypted with.

```sh
FINTRACK_ENCRYPTION_KEYS=1:<your key>
FINTRACK_ENCRYPTION_CURRENT_KEY_ID=1
```

## Rotating

1. Add a new key with a new id, and make it current.

   ```sh
   FINTRACK_ENCRYPTION_KEYS=1:<old key>;2:<new key>
   FINTRACK_ENCRYPTION_CURRENT_KEY_ID=2
   ```

2. Back up the new keyring.
3. Restart with `docker compose up -d`. FinTrack re-encrypts everything with the new key on start.

Once that's done you can remove the old key. Keep a copy while you still have database backups from before the
rotation, as they need it.
