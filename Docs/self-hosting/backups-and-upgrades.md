# Backups and upgrades

## Backups

Back up the database and your `.env` file. The database can't be read without the encryption keys in `.env`.

```sh
docker compose exec -T fintrack-postgres \
  sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -U postgres -Fc FinTrackDb' > fintrack-$(date +%F).dump
```

The database asks for its password even from inside its container, so the command passes the container's own. Keep
the single quotes, they make `$POSTGRES_PASSWORD` the container's rather than your shell's.

Run it on a schedule and keep copies off the server. To restore:

```sh
docker compose exec -T fintrack-postgres \
  sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" pg_restore -U postgres -d FinTrackDb --clean --if-exists' < fintrack.dump
```

Each user can also download their own data from the Your data page.

## Upgrading

1. Back up the database.
2. Set `FINTRACK_VERSION` to the new release.
3. Pull and restart.

   ```sh
   docker compose pull
   docker compose up -d
   ```

The database is updated on start. Older releases can't use it after that, so to roll back, restore your backup.
