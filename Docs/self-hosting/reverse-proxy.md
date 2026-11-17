# Reverse proxy

FinTrack serves plain HTTP on port 8080. Put a reverse proxy in front of it, such as Caddy, Traefik or nginx, if you
want HTTPS.

## Joining the proxy

The compose file adds FinTrack to your proxy's Docker network, `proxy-net` by default, or set `FINTRACK_PROXY_NETWORK`.
Your proxy can then reach it by name. With Caddy:

```
fintrack.example.com {
    reverse_proxy fintrackweb:8080
}
```

## Trusted proxies

Set `TRUSTED_PROXIES` to your proxy's address or network, such as `172.18.0.0/16`. `docker network inspect proxy-net`
shows the network's subnet.

Without it FinTrack sees every request as coming from the proxy, so rate limits are shared by everyone and logs show
the proxy's address. Forwarded headers from anywhere else are ignored.

## HTTPS

Once a browser has visited over HTTPS, it'll only use HTTPS for that address for the next year. So if you add HTTPS,
keep it.

FinTrack sets its own security headers, there's no need to add them at the proxy.
