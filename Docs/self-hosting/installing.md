# Installing

You'll need a machine with Docker Compose, and TrueLayer credentials for connecting banks, see
[Your bank connection](open-banking.md).

## Set up

1. Copy `Docker/docker-compose.yaml` and `Docker/.env.example` to a folder on your server. Rename `.env.example` to
   `.env`.
2. Fill in `.env`. [Configuration](configuration.md) covers every setting, but you'll need at least:
    - `FINTRACK_VERSION`, a tag from [Docker Hub](https://hub.docker.com/r/keeganwright12/fintrackweb/tags).
    - `FINTRACK_POSTGRES_PASSWORD`, any long random password.
    - Your [encryption keys](encryption-keys.md).
    - Your TrueLayer settings.
3. The compose file expects a reverse proxy on the `proxy-net` network. If you don't use one, remove the `proxy`
   network and publish port 8080 instead. See [Reverse proxy](reverse-proxy.md).
4. Start it.

   ```sh
   docker compose up -d
   ```

The database is set up on first start. `docker compose logs -f fintrackweb` shows how it's going.

## Your account

Open FinTrack and register. The first person to register becomes the admin, who can manage other users from the Users
page.

Anyone who can reach FinTrack can register. If it's open to the internet, consider putting access controls in front of
it at your reverse proxy.
