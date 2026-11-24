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

## Moving to PostgreSQL 18

Releases from the .NET 11 upgrade run PostgreSQL 18, where earlier ones ran 17. A major version can't open another's
data, and 18 keeps it somewhere new, so the compose file gives it a new volume, `FinTrack-Postgres18-data`. Your data
moves across with a backup and restore. The old volume is left as it was, so you can go back to it if anything goes
wrong.

1. While still on the old compose file, stop FinTrack and back up the database.

   ```sh
   docker compose stop fintrackweb
   docker compose exec -T fintrack-postgres \
     sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -U postgres -Fc FinTrackDb' > fintrack-pg17.dump
   docker compose down
   ```

2. Update the compose file to the new release's, then start only the database. It starts empty, on the new volume.

   ```sh
   docker compose up -d fintrack-postgres
   ```

3. Restore the backup into it, then start FinTrack.

   ```sh
   docker compose exec -T fintrack-postgres \
     sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" pg_restore -U postgres -d postgres --create' < fintrack-pg17.dump
   docker compose up -d
   ```

Check your data is all there before removing the old volume, `docker volume rm` with its name from
`docker volume ls`. Until then, to go back, use the previous compose file and release.
