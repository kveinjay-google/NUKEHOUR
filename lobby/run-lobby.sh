#!/bin/sh

set -eu

LOBBY_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
CONFIG_FILE=${NUKEHOUR_LOBBY_CONFIG:-"${LOBBY_ROOT}/lobby-config.env"}

if [ -f "${CONFIG_FILE}" ]; then
	while IFS='=' read -r key value; do
		case "${key}" in
		''|'#'*) continue ;;
		NUKEHOUR_LOBBY_ENVIRONMENT|NUKEHOUR_LOBBY_REGISTRATION_TOKEN|NUKEHOUR_LOBBY_ALLOWED_HOSTS|NUKEHOUR_LOBBY_HOST|NUKEHOUR_LOBBY_PORT|NUKEHOUR_LOBBY_MAX_BODY_BYTES|NUKEHOUR_LOBBY_PUBLIC_RATE_LIMIT|NUKEHOUR_LOBBY_SERVER_RATE_LIMIT|NUKEHOUR_LOBBY_RATE_WINDOW_SECONDS)
			export "${key}=${value}"
			;;
		*) printf >&2 'Unsupported Lobby configuration key: %s\n' "${key}"; exit 2 ;;
		esac
	done < "${CONFIG_FILE}"
fi

: "${NUKEHOUR_LOBBY_REGISTRATION_TOKEN:?NUKEHOUR_LOBBY_REGISTRATION_TOKEN is required}"
NUKEHOUR_LOBBY_HOST=${NUKEHOUR_LOBBY_HOST:-127.0.0.1}
NUKEHOUR_LOBBY_PORT=${NUKEHOUR_LOBBY_PORT:-8080}

case "${NUKEHOUR_LOBBY_PORT}" in *[!0-9]*|'') printf >&2 'NUKEHOUR_LOBBY_PORT must be an integer\n'; exit 2 ;; esac
[ "${NUKEHOUR_LOBBY_PORT}" -ge 1 ] && [ "${NUKEHOUR_LOBBY_PORT}" -le 65535 ] || {
	printf >&2 'NUKEHOUR_LOBBY_PORT must be 1..65535\n'
	exit 2
}

export PYTHONPATH="${LOBBY_ROOT}/src"
exec python3 -m uvicorn nukehour_lobby.app:app_from_environment \
	--factory \
	--host "${NUKEHOUR_LOBBY_HOST}" \
	--port "${NUKEHOUR_LOBBY_PORT}" \
	--proxy-headers \
	--forwarded-allow-ips=127.0.0.1 \
	--no-access-log
