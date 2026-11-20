# Encryption keys

FinTrack encrypts most of what it stores, such as amounts, descriptions, names, account details and bank tokens.

- Values are encrypted with AES-256-GCM, with a random nonce per value.
- Encrypted values that need searching, such as a transaction's id at the bank, also get a blind index, an
  HMAC-SHA256 of the value.
- Each row carries an integrity tag, see [Row integrity](#row-integrity).
- The encryption, blind index and row integrity keys are derived from your key with HKDF-SHA256, so they're never
  the same and your key itself is never used directly.

The keys live in your `.env` file, not the database.

> [!WARNING]
> Back up your keys somewhere away from the server, such as a password manager. Without them your data can't be read,
> even from a database backup.

## Passwords and sign in

- Passwords are never stored, only a PBKDF2-HMAC-SHA512 hash with 210,000 rounds. A hash made with fewer rounds is
  remade the next time its owner signs in.
- Sign-in cookies, and the links in confirmation and reset emails, are protected by ASP.NET Data Protection with
  AES-256-GCM. Its keys are kept in the database, encrypted with your `FINTRACK_ENCRYPTION_KEYS` key like the rest
  of your data, and rotating your key re-encrypts them too.

## Row integrity

Encryption keeps values secret, but on its own it doesn't stop someone with access to the database moving them
around. Each row of your data also carries an integrity tag, an HMAC-SHA256 over the row's id, who it belongs to,
what it belongs to, and its encrypted values, made with its own key derived from yours. FinTrack checks the tag
whenever it loads the row.

So someone who can change the database but doesn't have your keys can't, without it being caught:

- move a row to another user, such as an account, a debt or a passkey,
- move a row to another parent, such as a transaction to another account,
- copy an encrypted value from one row into another, such as an account number,
- copy a row under a new id.

It doesn't catch:

- **Anything done with your keys.** With the database and `FINTRACK_ENCRYPTION_KEYS` both, a valid tag can be made
  for anything.
- **An older copy of a whole row put back**, for example from a backup. Its tag was valid then and still is.
- **Deleted rows.**
- **The order of a list kept inside a row**, such as a split transaction's parts.
- **The users table itself**, which ASP.NET Identity writes often. Users' authenticator keys, recovery codes and
  passkeys are covered.

Tags are added to existing rows the first time FinTrack starts with this version, and remade under the new key when
you rotate.

### When a row fails its check

FinTrack refuses the row rather than showing it, so whatever reads it fails with an error. It logs
`Row {id} in {table} failed its integrity check, it was changed in the database outside FinTrack` at critical, and
counts it in `fintrack.row_integrity.failed`. See [Monitoring](monitoring.md#alerts) for the alert.

1. Treat it as a security problem. Something changed the database directly. Find out who or what has access to it.
2. Put the row back as it was, from a backup taken before the change. FinTrack can't fix the tag itself, as that
   would hide the change.
3. If it was you, for example editing a row by hand, undo the edit the same way.

A row that fails its check while FinTrack is re-encrypting after a key rotation stops it starting, with the same log
line naming the row.

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
