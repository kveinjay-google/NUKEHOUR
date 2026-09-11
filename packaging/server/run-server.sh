#!/bin/sh

set -eu

SERVER_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
CONFIG_FILE=${NUKEHOUR_SERVER_CONFIG:-"${SERVER_ROOT}/server-config.env"}

if [ -f "${CONFIG_FILE}" ]; then
	while IFS='=' read -r key value; do
		case "${key}" in
			''|'#'*) continue ;;
			Name|ListenAddress|ListenPort|Map|Password|MaxPlayers|AdvertiseLAN|RecordReplays|EnableSyncReports|EnableSingleplayer|RequireAuthentication|IdleTimeoutSeconds|StatusFile|SupportDir|OnlineLobbyUrl|OnlineLobbyServerId|NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL|OnlineLobbyPublicEndpoint|OnlineLobbyPublicPort|OnlineLobbyRegion|OnlineLobbyHeartbeatSeconds)
				export "${key}=${value}"
				;;
			*) echo >&2 "Unsupported server configuration key: ${key}"; exit 2 ;;
		esac
	done < "${CONFIG_FILE}"
fi

Name=${Name:-"NUKE HOUR Dedicated Server"}
ListenAddress=${ListenAddress:-"0.0.0.0"}
ListenPort=${ListenPort:-"1234"}
Map=${Map:-""}
Password=${Password:-""}
MaxPlayers=${MaxPlayers:-"8"}
AdvertiseLAN=${AdvertiseLAN:-"False"}
RecordReplays=${RecordReplays:-"False"}
EnableSyncReports=${EnableSyncReports:-"True"}
EnableSingleplayer=${EnableSingleplayer:-"False"}
RequireAuthentication=${RequireAuthentication:-"False"}
IdleTimeoutSeconds=${IdleTimeoutSeconds:-"900"}
StatusFile=${StatusFile:-"status/server-status.json"}
SupportDir=${SupportDir:-"${SERVER_ROOT}/support"}
OnlineLobbyUrl=${OnlineLobbyUrl:-""}
OnlineLobbyServerId=${OnlineLobbyServerId:-""}
NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL=${NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL:-""}
OnlineLobbyPublicEndpoint=${OnlineLobbyPublicEndpoint:-""}
OnlineLobbyPublicPort=${OnlineLobbyPublicPort:-"0"}
OnlineLobbyRegion=${OnlineLobbyRegion:-""}
OnlineLobbyHeartbeatSeconds=${OnlineLobbyHeartbeatSeconds:-"15"}

case "${ListenPort}" in *[!0-9]*|'') echo >&2 "ListenPort must be an integer"; exit 2 ;; esac
case "${MaxPlayers}" in *[!0-9]*|'') echo >&2 "MaxPlayers must be an integer"; exit 2 ;; esac
case "${IdleTimeoutSeconds}" in *[!0-9]*|'') echo >&2 "IdleTimeoutSeconds must be an integer"; exit 2 ;; esac
case "${OnlineLobbyPublicPort}" in *[!0-9]*|'') echo >&2 "OnlineLobbyPublicPort must be an integer"; exit 2 ;; esac
case "${OnlineLobbyHeartbeatSeconds}" in *[!0-9]*|'') echo >&2 "OnlineLobbyHeartbeatSeconds must be an integer"; exit 2 ;; esac
[ "${ListenPort}" -ge 1 ] && [ "${ListenPort}" -le 65535 ] || { echo >&2 "ListenPort must be 1..65535"; exit 2; }
[ "${MaxPlayers}" -ge 1 ] && [ "${MaxPlayers}" -le 64 ] || { echo >&2 "MaxPlayers must be 1..64"; exit 2; }
[ "${AdvertiseLAN}" = "False" ] || { echo >&2 "AdvertiseLAN=True is not implemented in Phase 2"; exit 2; }
if [ -n "${OnlineLobbyUrl}" ] && [ "${#NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL}" -lt 32 ]; then
	echo >&2 "NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL must contain at least 32 characters"
	exit 2
fi

mkdir -p "${SupportDir}"
export MOD_SEARCH_PATHS="${SERVER_ROOT}/mods"
export DOTNET_ROLL_FORWARD=${DOTNET_ROLL_FORWARD:-LatestMajor}
export NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL

set -- \
	"Engine.EngineDir=${SERVER_ROOT}" \
	"Engine.SupportDir=${SupportDir}" \
	"Game.Mod=ra2" \
	"Server.Name=${Name}" \
	"Server.ListenAddress=${ListenAddress}" \
	"Server.ListenPort=${ListenPort}" \
	"Server.Map=${Map}" \
	"Server.Password=${Password}" \
	"Server.MaxPlayers=${MaxPlayers}" \
	"Server.RecordReplays=${RecordReplays}" \
	"Server.EnableSyncReports=${EnableSyncReports}" \
	"Server.EnableSingleplayer=${EnableSingleplayer}" \
	"Server.RequireAuthentication=${RequireAuthentication}" \
	"Server.EnableGeoIP=False" \
	"Server.ShareAnonymizedIPs=False" \
	"Server.IdleTimeoutSeconds=${IdleTimeoutSeconds}" \
	"Server.StatusFile=${StatusFile}" \
	"Server.OnlineLobbyUrl=${OnlineLobbyUrl}" \
	"Server.OnlineLobbyServerId=${OnlineLobbyServerId}" \
	"Server.OnlineLobbyPublicEndpoint=${OnlineLobbyPublicEndpoint}" \
	"Server.OnlineLobbyPublicPort=${OnlineLobbyPublicPort}" \
	"Server.OnlineLobbyRegion=${OnlineLobbyRegion}" \
	"Server.OnlineLobbyHeartbeatSeconds=${OnlineLobbyHeartbeatSeconds}"

if [ -x "${SERVER_ROOT}/bin/OpenRA.Server" ]; then
	exec "${SERVER_ROOT}/bin/OpenRA.Server" "$@"
fi

exec dotnet "${SERVER_ROOT}/bin/OpenRA.Server.dll" "$@"
