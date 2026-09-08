#!/bin/sh
set -eu

repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
map=${OPENRA_V3_RUNTIME_AUDIT_MAP:-south-pacific}
lobby_commands=${OPENRA_V3_RUNTIME_AUDIT_LOBBY_COMMANDS:-}
if [ -z "$lobby_commands" ]; then
	lobby_commands='slot_bot Multi1 0 test|option fog False|option explored True'
fi
timeout_ticks=${OPENRA_V3_RUNTIME_AUDIT_TIMEOUT_TICKS:-900}
run_timeout_seconds=${OPENRA_V3_RUNTIME_AUDIT_RUN_TIMEOUT_SECONDS:-360}
term_grace_seconds=${OPENRA_V3_RUNTIME_AUDIT_TERM_GRACE_SECONDS:-5}
reap_grace_seconds=${OPENRA_V3_RUNTIME_AUDIT_REAP_GRACE_SECONDS:-5}
run_id=${OPENRA_V3_RUNTIME_AUDIT_RUN_ID:-$(date -u +%Y%m%dT%H%M%SZ)-$$}
support_dir=${OPENRA_V3_RUNTIME_AUDIT_SUPPORT_DIR:-"$HOME/Library/Application Support/OpenRA"}
runtime_dir=$support_dir/Logs/V3RuntimeAudit/$run_id
output_root=${OPENRA_V3_RUNTIME_AUDIT_OUTPUT:-$repo_root/docs/testing/results/v3-runtime-audit-$run_id}
summary=$runtime_dir/summary.json
evidence_manifest=$output_root/evidence.sha256
evidence_metadata=$output_root/evidence.json
legacy_fixture=$repo_root/docs/testing/fixtures/v3-legacy-8db6419/trajectory.csv
game_pid=

case "$run_id" in
	''|*[!A-Za-z0-9_-]*)
		echo "Run id must contain only letters, digits, '-' or '_': $run_id" >&2
		exit 2
		;;
esac

if [ "${#run_id}" -gt 80 ]; then
	echo "Run id must be at most 80 characters." >&2
	exit 2
fi

case "$timeout_ticks" in
	''|*[!0-9]*)
		echo "Tick timeout must be a positive integer: $timeout_ticks" >&2
		exit 2
		;;
esac

case "$run_timeout_seconds" in
	''|*[!0-9]*)
		echo "Run timeout must be a positive integer: $run_timeout_seconds" >&2
		exit 2
		;;
esac

if [ "$run_timeout_seconds" -le 0 ]; then
	echo "Run timeout must be greater than zero." >&2
	exit 2
fi

case "$term_grace_seconds" in
	''|*[!0-9]*|0)
		echo "TERM grace must be a positive integer: $term_grace_seconds" >&2
		exit 2
		;;
esac

case "$reap_grace_seconds" in
	''|*[!0-9]*|0)
		echo "Reap grace must be a positive integer: $reap_grace_seconds" >&2
		exit 2
		;;
esac

if [ ! -d "$support_dir" ]; then
	echo "OpenRA support directory does not exist: $support_dir" >&2
	exit 2
fi

if [ -e "$runtime_dir" ]; then
	echo "Refusing to reuse an existing runtime audit directory: $runtime_dir" >&2
	exit 2
fi

if [ -e "$output_root" ]; then
	echo "Refusing to overwrite an existing audit output: $output_root" >&2
	exit 2
fi

mkdir -p "$output_root"
launch_log=$output_root/launch.log

dotnet_bin=${OPENRA_V3_RUNTIME_AUDIT_DOTNET:-}
if [ -z "$dotnet_bin" ]; then
	if command -v dotnet >/dev/null 2>&1; then
		dotnet_bin=$(command -v dotnet)
	elif [ -x "$HOME/.dotnet/dotnet" ]; then
		dotnet_bin=$HOME/.dotnet/dotnet
	elif [ -x /opt/homebrew/bin/dotnet ]; then
		dotnet_bin=/opt/homebrew/bin/dotnet
	elif [ -x /usr/local/share/dotnet/dotnet ]; then
		dotnet_bin=/usr/local/share/dotnet/dotnet
	else
		echo "A dotnet runtime is required." >&2
		exit 2
	fi
fi

if [ ! -x "$dotnet_bin" ]; then
	echo "dotnet runtime is not executable: $dotnet_bin" >&2
	exit 2
fi

copy_report()
{
	[ -d "$runtime_dir" ] || return 0
	copy_status=0
	for report_file in progress.log results.csv trajectory.csv summary.json
	do
		if [ -f "$runtime_dir/$report_file" ]; then
			cp "$runtime_dir/$report_file" "$output_root/$report_file" || copy_status=$?
		fi
	done

	return "$copy_status"
}

game_process_matches()
{
	game_command=$(ps -p "$game_pid" -o command= 2>/dev/null || true)
	case "$game_command" in
		*OpenRA.dll*Game.LaunchInto=skirmish*) return 0 ;;
		*) return 1 ;;
	esac
}

wait_for_game_exit()
{
	remaining=$1
	while [ "$remaining" -gt 0 ] && kill -0 "$game_pid" 2>/dev/null
	do
		sleep 1
		remaining=$((remaining - 1))
	done

	if kill -0 "$game_pid" 2>/dev/null; then
		return 1
	fi

	wait "$game_pid" 2>/dev/null || true
	return 0
}

stop_game()
{
	[ -n "$game_pid" ] || return 0
	case "$game_pid" in
		*[!0-9]*)
			echo "Refusing to signal a non-numeric process id: $game_pid" >&2
			return 1
			;;
	esac

	if ! kill -0 "$game_pid" 2>/dev/null; then
		wait "$game_pid" 2>/dev/null || true
		return 0
	fi

	if ! game_process_matches; then
		echo "Refusing to signal PID $game_pid because its command no longer matches this audit: $game_command" >&2
		return 1
	fi

	kill -TERM "$game_pid"
	if wait_for_game_exit "$term_grace_seconds"; then
		return 0
	fi

	if ! game_process_matches; then
		if ! kill -0 "$game_pid" 2>/dev/null; then
			wait "$game_pid" 2>/dev/null || true
			return 0
		fi

		echo "Refusing to escalate PID $game_pid because its command no longer matches this audit: $game_command" >&2
		return 1
	fi

	kill -KILL "$game_pid"
	if wait_for_game_exit "$reap_grace_seconds"; then
		return 0
	fi

	echo "Owned game PID $game_pid did not reap within ${reap_grace_seconds}s after KILL." >&2
	return 1
}

cleanup()
{
	cleanup_status=$?
	trap - EXIT
	trap '' HUP INT TERM
	copy_status=0
	stop_status=0
	copy_report || copy_status=$?
	stop_game || stop_status=$?
	if [ "$cleanup_status" -eq 0 ] && [ "$copy_status" -ne 0 ]; then
		cleanup_status=$copy_status
	fi

	if [ "$cleanup_status" -eq 0 ] && [ "$stop_status" -ne 0 ]; then
		cleanup_status=$stop_status
	fi

	exit "$cleanup_status"
}

sha256_file()
{
	if command -v shasum >/dev/null 2>&1; then
		shasum -a 256 "$1" | awk '{ print $1 }'
	elif command -v sha256sum >/dev/null 2>&1; then
		sha256sum "$1" | awk '{ print $1 }'
	else
		echo "A SHA-256 tool (shasum or sha256sum) is required." >&2
		return 1
	fi
}

git_head()
{
	git -C "$repo_root" rev-parse HEAD
}

evidence_paths='OpenRA.Mods.RA2/Traits/V3RuntimeAudit.cs
OpenRA.Mods.RA2/Traits/BallisticMissile.cs
OpenRA.Mods.RA2/Activities/BallisticMissileFly.cs
mods/ra2/rules/soviet-vehicles.yaml
docs/testing/fixtures/v3-legacy-8db6419/trajectory.csv
docs/testing/fixtures/v3-legacy-8db6419/provenance.json
engine/bin/OpenRA.Mods.RA2.dll'

write_evidence()
{
	recorded_git_head=$(git_head)
	: > "$evidence_manifest"
	for relative_path in $evidence_paths
	do
		absolute_path=$repo_root/$relative_path
		if [ ! -f "$absolute_path" ]; then
			echo "Required V3 audit evidence file is missing: $absolute_path" >&2
			return 1
		fi

		file_hash=$(sha256_file "$absolute_path")
		printf '%s  %s\n' "$file_hash" "$relative_path" >> "$evidence_manifest"
	done

	manifest_hash=$(sha256_file "$evidence_manifest")
	cat > "$evidence_metadata" <<EOF
{
  "run_id": "$run_id",
  "git_head": "$recorded_git_head",
  "configuration": "Release",
  "build_command": "dotnet build OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore --nologo",
  "evidence_manifest_sha256": "$manifest_hash"
}
EOF
}

verify_evidence()
{
	current_git_head=$(git_head)
	if [ "$current_git_head" != "$recorded_git_head" ]; then
		echo "Git HEAD changed after the V3 runtime audit build." >&2
		return 1
	fi

	while read -r expected_hash relative_path
	do
		[ -n "$expected_hash" ] || continue
		actual_hash=$(sha256_file "$repo_root/$relative_path")
		if [ "$actual_hash" != "$expected_hash" ]; then
			echo "Evidence hash changed after build: $relative_path" >&2
			return 1
		fi
	done < "$evidence_manifest"

	actual_manifest_hash=$(sha256_file "$evidence_manifest")
	if [ "$actual_manifest_hash" != "$manifest_hash" ]; then
		echo "V3 runtime audit evidence manifest changed after it was recorded." >&2
		return 1
	fi
}

trap 'cleanup' EXIT
trap 'exit 130' HUP INT TERM

echo "Building the scoped RA2 runtime audit assembly in Release mode..."
DOTNET_ROLL_FORWARD=LatestMajor "$dotnet_bin" build \
	"$repo_root/OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj" \
	-c Release --no-restore --nologo

if [ ! -f "$repo_root/engine/bin/OpenRA.dll" ]; then
	echo "Built OpenRA engine not found after the scoped build." >&2
	exit 2
fi

write_evidence
verify_evidence

cd "$repo_root/engine"
OPENRA_V3_RUNTIME_AUDIT=true \
OPENRA_V3_RUNTIME_AUDIT_TIMEOUT_TICKS=$timeout_ticks \
OPENRA_V3_RUNTIME_AUDIT_RUN_ID=$run_id \
OPENRA_V3_RUNTIME_AUDIT_LEGACY_FIXTURE="$legacy_fixture" \
DOTNET_ROLL_FORWARD=LatestMajor \
"$dotnet_bin" bin/OpenRA.dll \
	Game.Mod=ra2 \
	Engine.EngineDir=.. \
	Engine.LaunchPath="$repo_root/launch-game.sh" \
	Engine.ModSearchPaths="./mods,$repo_root/mods" \
	Engine.SupportDir="$support_dir" \
	Graphics.Mode=Windowed \
	Graphics.WindowedSize=1024,768 \
	"Game.LaunchInto=skirmish $map $lobby_commands" >"$launch_log" 2>&1 &
game_pid=$!
cd "$repo_root"

deadline=$(($(date +%s) + run_timeout_seconds))
while [ "$(date +%s)" -lt "$deadline" ]
do
	if [ -f "$summary" ] && grep -F -q '"complete": true' "$summary"; then
		break
	fi

	if ! kill -0 "$game_pid" 2>/dev/null; then
		echo "OpenRA exited before the V3 runtime audit completed. See $launch_log" >&2
		exit 1
	fi

	sleep 1
done

if [ ! -f "$summary" ] || ! grep -F -q '"complete": true' "$summary"; then
	echo "V3 runtime audit did not complete within ${run_timeout_seconds}s: $run_id" >&2
	exit 1
fi

if ! grep -F -q '"failed": 0' "$summary" ||
	! grep -F -q '"passed": 9' "$summary" ||
	! grep -F -q '"total": 9' "$summary" ||
	! grep -F -q '"completed": 9' "$summary"; then
	echo "V3 runtime audit completed without the required 9/9 evidence: $summary" >&2
	exit 1
fi

verify_evidence
copy_report
echo "V3 runtime audit report: $output_root"
