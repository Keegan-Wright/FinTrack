# Backups and upgrades

## Backups

Back up the database and your `.env` file. The database can't be read without the encryption keys in `.env`.

```sh
docker compose exec -T fintrack-postgres pg_dump -U postgres -Fc FinTrackDb > fintrack-$(date +%F).dump
```

Run it on a schedule and keep copies off the server. To restore:

```sh
docker compose exec -T fintrack-postgres pg_restore -U postgres -d FinTrackDb --clean --if-exists < fintrack.dump
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
