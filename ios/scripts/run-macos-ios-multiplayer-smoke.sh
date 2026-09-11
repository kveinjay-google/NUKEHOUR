#!/bin/sh
set -eu

repo_root="$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)"
ios_device_id="${OPENRA_IOS_DEVICE_ID:?Set OPENRA_IOS_DEVICE_ID to one connected physical iOS device.}"
bundle_id="${OPENRA_IOS_BUNDLE_ID:-com.openra.ipad.personal}"
mac_launcher="${OPENRA_MACOS_MULTIPLAYER_LAUNCHER:-$repo_root/launch-game.sh}"
port="${OPENRA_IOS_MULTIPLAYER_SMOKE_PORT:-1234}"
dedicated_endpoint="${OPENRA_DEDICATED_SMOKE_ENDPOINT:-}"
online_lobby_url="${OPENRA_PHASE3_LOBBY_URL:-}"
online_server_id="${OPENRA_PHASE3_SERVER_ID:-}"
online_room_code=""
connection_port="$port"
map="${OPENRA_IOS_MULTIPLAYER_SMOKE_MAP:-south-pacific}"
required_world_ticks="${OPENRA_IOS_MULTIPLAYER_SMOKE_WORLD_TICKS:-1800}"
timeout_seconds="${OPENRA_IOS_MULTIPLAYER_SMOKE_TIMEOUT_SECONDS:-420}"
output_root="${OPENRA_PHASE12_OUTPUT:-$repo_root/artifacts/multiplayer-phase1.2}"
mac_support="${OPENRA_MACOS_SUPPORT_DIR:-$HOME/Library/Application Support/OpenRA}"

if [ -z "$dedicated_endpoint" ] && [ -z "$online_lobby_url" ] && [ "$port" != 1234 ]; then
	printf 'Phase 1.2 Direct IP acceptance requires TCP port 1234.\n' >&2
	exit 2
fi

if [ -n "$online_lobby_url" ] && [ -z "$online_server_id" ]; then
	printf 'Phase 3 Online acceptance requires OPENRA_PHASE3_SERVER_ID.\n' >&2
	exit 2
fi

if [ -n "$dedicated_endpoint" ]; then
	connection_port="${dedicated_endpoint##*:}"
	case "$connection_port" in
		''|*[!0-9]*)
			printf 'Dedicated endpoint must end with a numeric port: %s\n' "$dedicated_endpoint" >&2
			exit 2
			;;
	esac
fi

if [ ! -x "$mac_launcher" ]; then
	printf 'macOS launcher is not executable: %s\n' "$mac_launcher" >&2
	exit 2
fi

mac_lan_ip="${OPENRA_MACOS_LAN_IP:-$(ipconfig getifaddr en0 2>/dev/null || true)}"
if [ -z "$mac_lan_ip" ] && [ -z "$dedicated_endpoint" ] && [ -z "$online_lobby_url" ]; then
	printf 'Unable to determine the Mac LAN IPv4 address; set OPENRA_MACOS_LAN_IP.\n' >&2
	exit 2
fi

mkdir -p "$output_root"

fresh_identity()
{
	python3 - <<'PY'
from datetime import datetime, timezone
import secrets
print(datetime.now(timezone.utc).strftime("phase12-%Y%m%dT%H%M%SZ-") + secrets.token_hex(6))
print(secrets.token_hex(16))
print(secrets.token_hex(16))
print(secrets.token_hex(16))
PY
}

resolve_online_room_code()
{
	python3 - "$online_lobby_url" "$online_server_id" <<'PY'
import json, sys, urllib.parse, urllib.request
base, server_id = sys.argv[1:]
url = urllib.parse.urljoin(base.rstrip("/") + "/", "v1/rooms")
with urllib.request.urlopen(url, timeout=15) as response:
    payload = json.load(response)
matches = [room for room in payload.get("rooms", []) if room.get("serverId") == server_id]
if len(matches) != 1:
    raise SystemExit(f"expected exactly one Online room for server id, found {len(matches)}")
code = matches[0].get("roomCode", "")
if len(code) != 6:
    raise SystemExit("Online room did not return a valid six-character code")
print(code)
PY
}

ios_environment()
{
	role="$1"
	endpoint="$2"
	discovery="$3"
	python3 - "$role" "$endpoint" "$discovery" "$run_id" "$launch_nonce" "$ios_nonce" "$mac_nonce" \
		"$map" "$port" "$required_world_ticks" "$timeout_seconds" \
		"$online_lobby_url" "$online_server_id" "$online_room_code" <<'PY'
import json, sys
role, endpoint, discovery, run_id, launch, ios_nonce, mac_nonce, game_map, port, ticks, timeout, lobby, server_id, room_code = sys.argv[1:]
remote = mac_nonce
values = {
    "OPENRA_IOS_MULTIPLAYER_SMOKE_ROLE": role,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_DISCOVERY_MODE": discovery,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_REQUIRE_GAMEPLAY": "true",
    "OPENRA_IOS_MULTIPLAYER_SMOKE_MAP": game_map,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_PORT": port,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_RUN_ID": run_id,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_WORLD_TICKS": ticks,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_TIMEOUT_SECONDS": timeout,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_LAUNCH_NONCE": launch,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_PARTICIPANT_NONCE": ios_nonce,
    "OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_REMOTE_NONCE": remote,
}
if endpoint:
    values["OPENRA_IOS_MULTIPLAYER_SMOKE_ENDPOINT"] = endpoint
if lobby:
    values["OPENRA_IOS_MULTIPLAYER_SMOKE_ONLINE_LOBBY_URL"] = lobby
if discovery == "online-list":
    values["OPENRA_IOS_MULTIPLAYER_SMOKE_ONLINE_SERVER_ID"] = server_id
if discovery == "online-code":
    values["OPENRA_IOS_MULTIPLAYER_SMOKE_ONLINE_ROOM_CODE"] = room_code
print(json.dumps(values, separators=(",", ":")))
PY
}

copy_ios_journal()
{
	role="$1"
	destination="$2"
	temporary="$destination.incoming"
	if xcrun devicectl device copy from --device "$ios_device_id" \
		--domain-type appDataContainer --domain-identifier "$bundle_id" \
		--source "Documents/OpenRA/Logs/MultiplayerSmoke/$run_id/$role.json" \
		--destination "$temporary" --timeout 15 >"$direction_dir/copy-ios.log" 2>&1; then
		mv "$temporary" "$destination"
		return 0
	fi
	return 1
}

launch_ios()
{
	role="$1"
	endpoint="$2"
	discovery="$3"
	environment="$(ios_environment "$role" "$endpoint" "$discovery")"
	xcrun devicectl device process launch --device "$ios_device_id" --terminate-existing \
		--timeout 30 --json-output "$direction_dir/launch-ios.json" \
		--environment-variables "$environment" "$bundle_id" \
		>"$direction_dir/launch-ios.log" 2>&1
}

launch_mac()
{
	role="$1"
	endpoint="$2"
	discovery="$3"
	OPENRA_IOS_MULTIPLAYER_SMOKE_ROLE="$role" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_DISCOVERY_MODE="$discovery" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_REQUIRE_GAMEPLAY=true \
	OPENRA_IOS_MULTIPLAYER_SMOKE_ENDPOINT="$endpoint" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_ONLINE_LOBBY_URL="$online_lobby_url" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_ONLINE_SERVER_ID="$online_server_id" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_ONLINE_ROOM_CODE="$online_room_code" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_MAP="$map" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_PORT="$port" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_RUN_ID="$run_id" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_WORLD_TICKS="$required_world_ticks" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_TIMEOUT_SECONDS="$timeout_seconds" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_LAUNCH_NONCE="$launch_nonce" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_PARTICIPANT_NONCE="$mac_nonce" \
	OPENRA_IOS_MULTIPLAYER_SMOKE_EXPECTED_REMOTE_NONCE="$ios_nonce" \
		"$mac_launcher" >"$direction_dir/macos.log" 2>&1 &
	mac_pid="$!"
}

ios_pid()
{
	python3 - "$direction_dir/launch-ios.json" <<'PY'
import json, sys
def find(value):
    if isinstance(value, dict):
        for key, child in value.items():
            if key.lower() in {"processidentifier", "pid"} and isinstance(child, int):
                return child
        for child in value.values():
            result = find(child)
            if result is not None:
                return result
    if isinstance(value, list):
        for child in value:
            result = find(child)
            if result is not None:
                return result
with open(sys.argv[1], encoding="utf-8") as stream:
    result = find(json.load(stream))
print(result or "")
PY
}

cleanup_direction()
{
	set +e
	if [ -n "${mac_pid:-}" ]; then
		kill "$mac_pid" 2>/dev/null || true
		wait "$mac_pid" 2>/dev/null || true
	fi
	if [ -s "$direction_dir/launch-ios.json" ]; then
		pid="$(ios_pid 2>/dev/null || true)"
		if [ -n "$pid" ]; then
			xcrun devicectl device process terminate --device "$ios_device_id" --pid "$pid" \
				--kill --timeout 15 >"$direction_dir/terminate-ios.log" 2>&1 || true
		fi
	fi
	set -e
}

private_ios_address()
{
	python3 - "$1" "$2" <<'PY'
import ipaddress, json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    report = json.load(stream)
candidates = []
for value in report.get("localLanAddresses", "").split(","):
    try:
        address = ipaddress.ip_address(value)
    except ValueError:
        continue
    if address.version == 4 and address.is_private and not address.is_loopback and not address.is_link_local:
        candidates.append(address)
if candidates:
    mac = ipaddress.ip_address(sys.argv[2])
    def common_prefix(address, peer):
        difference = int(address) ^ int(peer)
        return 32 if difference == 0 else 32 - difference.bit_length()
    print(max(candidates, key=lambda address: common_prefix(address, mac)))
PY
}

assert_journal()
{
	journal="$1"
	role="$2"
	expected_discovery="$3"
	expected_stage="$4"
	python3 - "$journal" "$role" "$run_id" "$required_world_ticks" \
		"$expected_discovery" "$expected_stage" <<'PY'
import json, sys
path, role, run_id, ticks, expected_discovery, expected_stage = sys.argv[1:]
with open(path, encoding="utf-8") as stream:
    report = json.load(stream)
errors = []
for key, expected in {
    "status": "PASSED", "role": role, "runId": run_id,
    "discoveryMode": expected_discovery, "discoveryStage": expected_stage,
    "gameplayRequired": True,
    "outOfSync": False, "orderRejectionObserved": False,
}.items():
    if report.get(key) != expected:
        errors.append(f"{key}={report.get(key)!r}")
for key in (
    "remoteIdentityVerified", "remoteAcknowledgedLocalIdentity",
    "mapBlockIssued" if role == "client" else "blockedStartRejected",
    "mapReadyRestoreIssued" if role == "client" else "blockedStartIssued",
    "gameplayOptionsObserved", "spawnObserved", "selectObserved",
    "movementObserved", "resourceUpdateObserved", "productionObserved",
    "completedProductionObserved", "attackObserved",
):
    if report.get(key) is not True:
        errors.append(f"{key}={report.get(key)!r}")
if int(report.get("clients", 0)) != 2:
    errors.append(f"clients={report.get('clients')!r}")
if int(report.get("worldTick", 0)) < int(ticks):
    errors.append(f"worldTick={report.get('worldTick')!r}")
if not report.get("runtimeProfile") or not report.get("runtimeContract"):
    errors.append("missing canonical runtime identity")
if report.get("resourceCapability") != "ra2-presentation-capability-v2":
    errors.append(f"resourceCapability={report.get('resourceCapability')!r}")
if errors:
    raise SystemExit(role + " journal failed: " + ", ".join(errors))
PY
}

assert_pair()
{
	python3 - "$1" "$2" "$connection_port" "$online_server_id" "$online_room_code" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    host = json.load(stream)
with open(sys.argv[2], encoding="utf-8") as stream:
    client = json.load(stream)
for key in ("runtimeHash", "runtimeContract", "modCompatibility", "resourceCapability", "map"):
    if not host.get(key) or host.get(key) != client.get(key):
        raise SystemExit(f"pair mismatch for {key}: {host.get(key)!r} != {client.get(key)!r}")
if sys.argv[4]:
    endpoints = {host.get("endpoint", ""), client.get("endpoint", "")}
    expected = {"online-list:" + sys.argv[4], "online-code:" + sys.argv[5]}
    if endpoints != expected:
        raise SystemExit(f"clients did not use Online list/code discovery: {endpoints!r}")
elif not str(client.get("endpoint", "")).endswith(":" + sys.argv[3]):
    raise SystemExit(f"client did not use Direct IP:{sys.argv[3]}: {client.get('endpoint')!r}")
PY
}

wait_for_pair()
{
	ios_role="$1"
	mac_role="$2"
	ios_discovery="$3"
	mac_discovery="$4"
	ios_stage="$5"
	mac_stage="$6"
	ios_journal="$direction_dir/ios-$ios_role.json"
	mac_source="$mac_support/Logs/MultiplayerSmoke/$run_id/$mac_role.json"
	mac_journal="$direction_dir/macos-$mac_role.json"
	deadline=$(($(date +%s) + timeout_seconds + 45))
	while [ "$(date +%s)" -lt "$deadline" ]; do
		copy_ios_journal "$ios_role" "$ios_journal" || true
		if [ -f "$mac_source" ]; then
			cp "$mac_source" "$mac_journal"
		fi
		if [ -s "$ios_journal" ] && [ -s "$mac_journal" ] &&
			grep -q '"status": "PASSED"' "$ios_journal" &&
			grep -q '"status": "PASSED"' "$mac_journal"; then
			assert_journal "$ios_journal" "$ios_role" "$ios_discovery" "$ios_stage"
			assert_journal "$mac_journal" "$mac_role" "$mac_discovery" "$mac_stage"
			if [ "$ios_role" = host ]; then
				assert_pair "$ios_journal" "$mac_journal"
			else
				assert_pair "$mac_journal" "$ios_journal"
			fi
			return 0
		fi
		if { [ -s "$ios_journal" ] && grep -q '"status": "FAILED"' "$ios_journal"; } ||
			{ [ -s "$mac_journal" ] && grep -q '"status": "FAILED"' "$mac_journal"; }; then
			return 1
		fi
		sleep 2
	done
	return 1
}

run_direction()
{
	direction="$1"
	values="$(fresh_identity)"
	run_id="$(printf '%s\n' "$values" | sed -n '1p')"
	launch_nonce="$(printf '%s\n' "$values" | sed -n '2p')"
	ios_nonce="$(printf '%s\n' "$values" | sed -n '3p')"
	mac_nonce="$(printf '%s\n' "$values" | sed -n '4p')"
	direction_dir="$output_root/$direction-$run_id"
	mkdir -p "$direction_dir"
	mac_pid=""
	trap cleanup_direction EXIT INT TERM

	if [ "$direction" = online-macos-ios ]; then
		online_room_code="$(resolve_online_room_code)"
		launch_mac host "" online-list
		sleep 3
		launch_ios client "" online-code
		wait_for_pair client host onlinecode onlinelist \
			online-room-code-matched online-room-list-matched
	elif [ "$direction" = dedicated-macos-ios ]; then
		launch_mac host "$dedicated_endpoint" direct
		sleep 3
		launch_ios client "$dedicated_endpoint" direct
		wait_for_pair client host direct direct direct direct
	elif [ "$direction" = ios-host-macos-client ]; then
		launch_ios host "" auto
		host_journal="$direction_dir/ios-host-bootstrap.json"
		deadline=$(($(date +%s) + 90))
		ios_lan_ip=""
		while [ "$(date +%s)" -lt "$deadline" ]; do
			if copy_ios_journal host "$host_journal"; then
				ios_lan_ip="$(private_ios_address "$host_journal" "$mac_lan_ip")"
				[ -z "$ios_lan_ip" ] || break
			fi
			sleep 2
		done
		if [ -z "$ios_lan_ip" ]; then
			printf 'iOS host did not publish a private LAN IPv4 address.\n' >&2
			return 1
		fi
		launch_mac client "$ios_lan_ip:$port" direct
		wait_for_pair host client auto direct host direct
	else
		launch_mac host "" auto
		sleep 3
		launch_ios client "$mac_lan_ip:$port" direct
		wait_for_pair client host direct auto direct host
	fi

	cleanup_direction
	trap - EXIT INT TERM
	printf '%s PASS (%s)\n' "$direction" "$direction_dir"
}

if [ -n "$online_lobby_url" ]; then
	run_direction online-macos-ios
	printf 'Phase 3 Online Room List + Room Code physical session passed: %s\n' "$output_root"
elif [ -n "$dedicated_endpoint" ]; then
	run_direction dedicated-macos-ios
	printf 'Dedicated Server Apple Direct IP physical session passed: %s\n' "$output_root"
else
	run_direction ios-host-macos-client
	run_direction macos-host-ios-client
	printf 'Phase 1.2 Apple Direct IP physical matrix passed: %s\n' "$output_root"
fi
