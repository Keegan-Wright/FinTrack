#!/usr/bin/env bash
# Points the AppHost's TrueLayer user secrets at the live or sandbox environment.
#
# Usage: Scripts/truelayer-env.sh --live | --sandbox
#
# Prompts for the client id, then the client secret. Leave either blank to keep the current value.
# Restart the AppHost afterwards for the change to take effect.
set -euo pipefail

apphost_project="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Src/FinanceTracker.AppHost"

usage() {
    echo "Usage: $0 --live | --sandbox" >&2
    exit 1
}

[[ $# -eq 1 ]] || usage

case "$1" in
    --live)
        environment="live"
        auth_url="https://auth.truelayer.com/"
        data_url="https://api.truelayer.com/data/"
        ;;
    --sandbox)
        environment="sandbox"
        auth_url="https://auth.truelayer-sandbox.com/"
        data_url="https://api.truelayer-sandbox.com/data/"
        ;;
    *)
        usage
        ;;
esac

set_secret() {
    dotnet user-secrets set "OpenBanking:TrueLayer:$1" "$2" --project "$apphost_project" > /dev/null
}

read -r -p "TrueLayer $environment client id (blank to keep current): " client_id
read -r -s -p "TrueLayer $environment client secret (blank to keep current): " client_secret
echo

set_secret BaseAuthUrl "$auth_url"
set_secret BaseDataUrl "$data_url"

if [[ -n "$client_id" ]]; then
    set_secret ClientId "$client_id"
fi

if [[ -n "$client_secret" ]]; then
    set_secret ClientSecret "$client_secret"
fi

echo "TrueLayer set to $environment. Restart the AppHost to pick it up."
