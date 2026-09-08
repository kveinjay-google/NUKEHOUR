#!/bin/sh
set -eu

repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
map=${OPENRA_IMPACT_EFFECT_AUDIT_MAP:-south-pacific}
lobby_commands=${OPENRA_IMPACT_EFFECT_AUDIT_LOBBY_COMMANDS:-}
if [ -z "$lobby_commands" ]; then
	lobby_commands='slot_bot Multi1 0 test|option fog False|option explored True'
fi
timeout_ticks=${OPENRA_IMPACT_EFFECT_AUDIT_TIMEOUT_TICKS:-300}
run_timeout_seconds=${OPENRA_IMPACT_EFFECT_AUDIT_RUN_TIMEOUT_SECONDS:-480}
term_grace_seconds=${OPENRA_IMPACT_EFFECT_AUDIT_TERM_GRACE_SECONDS:-5}
reap_grace_seconds=${OPENRA_IMPACT_EFFECT_AUDIT_REAP_GRACE_SECONDS:-5}
run_id=${OPENRA_IMPACT_EFFECT_AUDIT_RUN_ID:-$(date -u +%Y%m%dT%H%M%SZ)-$$}
support_dir=${OPENRA_IMPACT_EFFECT_AUDIT_SUPPORT_DIR:-"$HOME/Library/Application Support/OpenRA"}
runtime_dir=$support_dir/Logs/ImpactEffectsRuntimeAudit/$run_id
output_root=${OPENRA_IMPACT_EFFECT_AUDIT_OUTPUT:-$repo_root/docs/testing/results/impact-effects-runtime-audit-$run_id}
summary=$runtime_dir/summary.json
lock_dir=$repo_root/.openra-runtime-audit.lock
prebuild_evidence=$output_root/prebuild-inputs.sha256
build_evidence=$output_root/build-evidence.sha256
evidence_manifest=$output_root/evidence.sha256
evidence_metadata=$output_root/evidence.json
evidence_manifest_tmp=$evidence_manifest.tmp
evidence_metadata_tmp=$evidence_metadata.tmp
report_staging=$output_root/.report-staging
staged_summary=$report_staging/summary.json
game_pid=
game_identity=
current_game_identity=
launch_identity=
current_launch_identity=
launch_identity_pending=false
identity_capture_attempts=5
runner_pid=$$
lock_owned=false
lock_identity=
recorded_git_head=
prebuild_manifest_hash=
build_manifest_hash=
report_copied=false
python_bin=
launch_log=$output_root/launch.log

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

for numeric_setting in "$timeout_ticks" "$run_timeout_seconds" "$term_grace_seconds" "$reap_grace_seconds"
do
	case "$numeric_setting" in
		''|*[!0-9]*|0)
			echo "Audit timeouts must be positive integers: $numeric_setting" >&2
			exit 2
			;;
	esac
done

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

copy_report()
{
	[ -d "$runtime_dir" ] || return 0
	mkdir -p "$report_staging"
	copy_status=0
	for report_file in progress.log cases.csv effects.csv performance.csv resolved-weapons.json \
		blocked-retail-assets.txt
	do
		if [ -f "$runtime_dir/$report_file" ]; then
			cp "$runtime_dir/$report_file" "$report_staging/$report_file" || copy_status=$?
		fi
	done

	for image_directory in baselines screenshots
	do
		if [ ! -d "$runtime_dir/$image_directory" ]; then
			continue
		fi

		mkdir -p "$report_staging/$image_directory"
		for screenshot_file in "$runtime_dir/$image_directory"/*.png
		do
			[ -f "$screenshot_file" ] || continue
			cp "$screenshot_file" "$report_staging/$image_directory/" || copy_status=$?
		done
	done

	if [ -f "$summary" ]; then
		cp "$summary" "$staged_summary" || copy_status=$?
	fi

	return "$copy_status"
}

copy_report_once()
{
	[ "$report_copied" = false ] || return 0
	copy_report || return $?
	report_copied=true
}

publish_staged_report()
{
	[ "$report_copied" = true ] || return 1
	[ -f "$staged_summary" ] || {
		echo "Impact runtime audit staged report has no summary.json." >&2
		return 1
	}

	for report_file in progress.log cases.csv effects.csv performance.csv resolved-weapons.json \
		blocked-retail-assets.txt
	do
		if [ -f "$report_staging/$report_file" ]; then
			mv "$report_staging/$report_file" "$output_root/$report_file"
		fi
	done

	for image_directory in baselines screenshots
	do
		if [ -d "$report_staging/$image_directory" ]; then
			mv "$report_staging/$image_directory" "$output_root/$image_directory"
		fi
	done

	mv "$staged_summary" "$output_root/summary.json"
	rmdir "$report_staging"
}

validate_staged_success_images()
{
	baseline_count=0
	if [ -d "$report_staging/baselines" ]; then
		baseline_count=$(find "$report_staging/baselines" -type f -name '*.png' | wc -l | tr -d ' ')
	fi
	if [ "$baseline_count" -ne 16 ]; then
		echo "Impact runtime audit expected 16 baseline screenshots but staged $baseline_count." >&2
		return 1
	fi

	screenshot_count=0
	if [ -d "$report_staging/screenshots" ]; then
		screenshot_count=$(find "$report_staging/screenshots" -type f -name '*.png' | wc -l | tr -d ' ')
	fi
	if [ "$screenshot_count" -ne 16 ]; then
		echo "Impact runtime audit expected 16 screenshots but staged $screenshot_count." >&2
		return 1
	fi
}

directory_identity()
{
	if stat -f '%d:%i' "$1" >/dev/null 2>&1; then
		stat -f '%d:%i' "$1"
	elif stat -c '%d:%i' "$1" >/dev/null 2>&1; then
		stat -c '%d:%i' "$1"
	else
		return 1
	fi
}

read_game_identity()
{
	OPENRA_IMPACT_EFFECT_AUDIT_RUNNER_PID=$runner_pid \
		ps -p "$game_pid" -o ppid= -o lstart= -o command= 2>/dev/null
}

launch_identity_from_process_identity()
{
	process_identity=$1
	printf '%s\n' "$process_identity" |
		awk 'NF >= 6 { print $1 "|" $2 " " $3 " " $4 " " $5 " " $6; exit }'
}

identity_has_expected_command()
{
	identity=$1
	case "$identity" in
		*"$dotnet_bin"*OpenRA.dll*Game.LaunchInto=skirmish*) return 0 ;;
		*) return 1 ;;
	esac
}

capture_initial_game_identity()
{
	identity_capture_attempt=1
	while [ "$identity_capture_attempt" -le "$identity_capture_attempts" ] &&
		[ "$(date +%s)" -lt "$deadline" ]
	do
		candidate_identity=$(read_game_identity || true)
		candidate_launch_identity=$(launch_identity_from_process_identity "$candidate_identity")
		if [ -n "$candidate_launch_identity" ]; then
			if [ -z "$launch_identity" ]; then
				case "$candidate_launch_identity" in
					"$runner_pid"'|'*) launch_identity=$candidate_launch_identity ;;
				esac
			fi

			if [ "$candidate_launch_identity" = "$launch_identity" ] &&
				identity_has_expected_command "$candidate_identity"; then
				game_identity=$candidate_identity
				launch_identity_pending=false
				return 0
			fi
		fi

		if ! game_process_is_running; then
			return 2
		fi

		if [ "$identity_capture_attempt" -lt "$identity_capture_attempts" ] &&
			[ "$(date +%s)" -lt "$deadline" ]; then
			sleep 1
		fi
		identity_capture_attempt=$((identity_capture_attempt + 1))
	done

	return 1
}

game_process_matches()
{
	current_game_identity=$(read_game_identity || true)
	[ -n "$current_game_identity" ] || return 1
	[ "$current_game_identity" = "$game_identity" ] || return 1
	identity_has_expected_command "$current_game_identity"
}

launch_process_matches()
{
	current_game_identity=$(read_game_identity || true)
	current_launch_identity=$(launch_identity_from_process_identity "$current_game_identity")
	[ -n "$launch_identity" ] || return 1
	[ -n "$current_launch_identity" ] || return 1
	[ "$current_launch_identity" = "$launch_identity" ]
}

game_process_is_running()
{
	if ! kill -0 "$game_pid" 2>/dev/null; then
		wait "$game_pid" 2>/dev/null || true
		return 1
	fi

	game_state=$(ps -p "$game_pid" -o stat= 2>/dev/null || true)
	case "$game_state" in
		*Z*)
			wait "$game_pid" 2>/dev/null || true
			return 1
			;;
	esac

	return 0
}

wait_for_game_exit()
{
	remaining=$1
	while [ "$remaining" -gt 0 ] && game_process_is_running
	do
		sleep 1
		remaining=$((remaining - 1))
	done

	if game_process_is_running; then
		return 1
	fi

	return 0
}

signal_game()
{
	signal_name=$1
	if ! game_process_matches; then
		if ! game_process_is_running; then
			return 2
		fi

		echo "Refusing to signal PID $game_pid because its identity no longer matches this audit: $current_game_identity" >&2
		return 1
	fi

	if kill -"$signal_name" "$game_pid" 2>/dev/null; then
		return 0
	fi

	if ! game_process_is_running; then
		return 2
	fi

	echo "Unable to send $signal_name to owned game PID $game_pid." >&2
	return 1
}

signal_launch_game()
{
	signal_name=$1
	if ! launch_process_matches; then
		if ! game_process_is_running; then
			return 2
		fi

		echo "Refusing to signal PID $game_pid because its launch identity no longer matches this audit: $current_launch_identity" >&2
		return 1
	fi

	if kill -"$signal_name" "$game_pid" 2>/dev/null; then
		return 0
	fi

	if ! game_process_is_running; then
		return 2
	fi

	echo "Unable to send $signal_name to launch-owned game PID $game_pid." >&2
	return 1
}

signal_owned_game()
{
	if [ "$launch_identity_pending" = true ]; then
		signal_launch_game "$1"
	else
		signal_game "$1"
	fi
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

	if ! game_process_is_running; then
		return 0
	fi

	term_status=0
	signal_owned_game TERM || term_status=$?
	case "$term_status" in
		0) ;;
		2) return 0 ;;
		*) return "$term_status" ;;
	esac
	if wait_for_game_exit "$term_grace_seconds"; then
		return 0
	fi

	kill_status=0
	signal_owned_game KILL || kill_status=$?
	case "$kill_status" in
		0) ;;
		2) return 0 ;;
		*) return "$kill_status" ;;
	esac
	if wait_for_game_exit "$reap_grace_seconds"; then
		return 0
	fi

	echo "Owned game PID $game_pid did not reap within ${reap_grace_seconds}s after KILL." >&2
	return 1
}

release_lock()
{
	[ "$lock_owned" = true ] || return 0
	current_lock_identity=$(directory_identity "$lock_dir" 2>/dev/null || true)
	if [ -z "$lock_identity" ] || [ "$current_lock_identity" != "$lock_identity" ]; then
		echo "Refusing to remove runtime audit lock because its directory identity changed: $lock_dir" >&2
		return 1
	fi

	if rmdir "$lock_dir" 2>/dev/null; then
		lock_owned=false
		return 0
	fi

	echo "Refusing to remove a non-empty or replaced runtime audit lock: $lock_dir" >&2
	return 1
}

cleanup()
{
	cleanup_status=$?
	trap - EXIT
	trap '' HUP INT TERM
	stop_status=0
	copy_status=0
	lock_status=0
	stop_game || stop_status=$?
	copy_report_once || copy_status=$?
	release_lock || lock_status=$?
	if [ "$cleanup_status" -eq 0 ] && [ "$stop_status" -ne 0 ]; then
		cleanup_status=$stop_status
	fi
	if [ "$cleanup_status" -eq 0 ] && [ "$copy_status" -ne 0 ]; then
		cleanup_status=$copy_status
	fi
	if [ "$cleanup_status" -eq 0 ] && [ "$lock_status" -ne 0 ]; then
		cleanup_status=$lock_status
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

strict_summary_json()
{
	summary_mode=$1
	summary_path=$2
	"$python_bin" - "$summary_mode" "$summary_path" <<'PY'
import json
import math
import sys


class DuplicateKeyError(ValueError):
    pass


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise DuplicateKeyError(f"duplicate key: {key}")
        result[key] = value
    return result


def reject_constant(value):
    raise ValueError(f"non-finite JSON constant: {value}")


def require_type(document, name, expected_type):
    value = document[name]
    if type(value) is not expected_type:
        raise ValueError(f"{name} must be {expected_type.__name__}")
    return value


def require_int(document, name):
    value = require_type(document, name, int)
    if value < 0:
        raise ValueError(f"{name} must be non-negative")
    return value


def require_metrics(document, name, metric_names):
    metrics = require_type(document, name, dict)
    if set(metrics) != metric_names:
        raise ValueError(f"{name} must contain exactly {sorted(metric_names)}")
    for metric_name, value in metrics.items():
        if type(value) not in (int, float):
            raise ValueError(f"{name}.{metric_name} must be a number")
        if type(value) is float and not math.isfinite(value):
            raise ValueError(f"{name}.{metric_name} must be finite")


mode, path = sys.argv[1:3]
try:
    with open(path, "r", encoding="utf-8") as stream:
        document = json.load(
            stream,
            object_pairs_hook=unique_object,
            parse_constant=reject_constant,
        )
    if type(document) is not dict:
        raise ValueError("top-level summary must be an object")
    if "complete" not in document or "status" not in document:
        raise ValueError("summary requires complete and status")
    complete = require_type(document, "complete", bool)
    status = require_type(document, "status", str)

    if mode == "probe":
        raise SystemExit(0 if complete else 1)
    if mode != "validate":
        raise ValueError(f"unknown validation mode: {mode}")
    if not complete:
        raise ValueError("final summary must be complete")

    expected_fields = {
        "complete", "status", "functional_status", "performance_status",
        "total", "completed", "passed", "failed", "screenshots",
        "burst_complete", "burst_impacts", "burst_sprite_schedules",
        "burst_sprite_added", "burst_sprite_rendered", "burst_sprite_completed",
        "burst_target_sprite_peak", "burst_global_sprite_peak", "burst_cleanup_ticks",
        "sample_count", "performance_sample_count", "performance_p95",
        "performance_max", "audio_backend_evidence", "audio_backend_limitation",
        "detail",
    }
    actual_fields = set(document)
    if actual_fields != expected_fields:
        missing = sorted(expected_fields - actual_fields)
        extra = sorted(actual_fields - expected_fields)
        raise ValueError(f"summary fields mismatch; missing={missing} extra={extra}")

    for name in (
        "functional_status", "performance_status", "audio_backend_evidence",
        "audio_backend_limitation", "detail",
    ):
        require_type(document, name, str)
    require_type(document, "burst_complete", bool)
    for name in (
        "total", "completed", "passed", "failed", "screenshots",
        "burst_impacts", "burst_sprite_schedules", "burst_sprite_added",
        "burst_sprite_rendered", "burst_sprite_completed", "burst_target_sprite_peak",
        "burst_global_sprite_peak", "burst_cleanup_ticks", "sample_count",
        "performance_sample_count",
    ):
        require_int(document, name)

    metric_names = {"render", "world_tick", "render_prepare", "render_flip", "batches"}
    require_metrics(document, "performance_p95", metric_names)
    require_metrics(document, "performance_max", metric_names)

    if status == "PASSED":
        required_values = {
            "functional_status": "PASSED",
            "performance_status": "RECORDED_FOR_REVIEW",
            "total": 16,
            "completed": 16,
            "passed": 16,
            "failed": 0,
            "screenshots": 16,
            "burst_complete": True,
            "burst_impacts": 16,
            "burst_sprite_schedules": 64,
            "burst_sprite_added": 64,
            "burst_sprite_rendered": 64,
            "burst_sprite_completed": 64,
        }
        for name, expected in required_values.items():
            if document[name] != expected:
                raise ValueError(f"{name} must equal {expected!r}")
        if document["sample_count"] <= 0:
            raise ValueError("sample_count must be greater than zero")
        if document["performance_sample_count"] != document["sample_count"]:
            raise ValueError("performance_sample_count must equal sample_count")
    elif status == "BLOCKED_RETAIL_ASSETS":
        if document["functional_status"] != status:
            raise ValueError("blocked functional_status must match status")
        if document["performance_status"] != "NOT_COMPLETED":
            raise ValueError("blocked performance_status must be NOT_COMPLETED")
    elif status == "FAILED":
        if document["functional_status"] != status:
            raise ValueError("failed functional_status must match status")
        if document["performance_status"] != "NOT_COMPLETED":
            raise ValueError("failed performance_status must be NOT_COMPLETED")
    else:
        raise ValueError(f"unsupported complete status: {status}")

    print(status)
except SystemExit:
    raise
except Exception as error:
    print(f"Invalid impact runtime audit summary JSON: {error}", file=sys.stderr)
    raise SystemExit(2)
PY
}

git_head()
{
	git -C "$repo_root" rev-parse HEAD
}

static_input_paths='packaging/run_impact_effects_runtime_audit.sh
OpenRA.Mods.RA2/Traits/ImpactEffectsRuntimeAudit.cs
OpenRA.Mods.RA2/Traits/IosHighUnitBenchmark.cs
engine/OpenRA.Mods.Common/Warheads/CreateEffectWarhead.cs
mods/ra2/weapons/explosions.yaml
mods/ra2/weapons/misc.yaml
mods/ra2/sequences/misc.yaml
mods/ra2/bits/animations/nc-impact-core.png
mods/ra2/bits/animations/nc-impact-ring.png
mods/ra2/bits/animations/nc-impact-debris.png'

built_dependency_paths='engine/bin/OpenRA.dll
engine/bin/OpenRA.Game.dll
engine/bin/OpenRA.Platforms.Default.dll
engine/bin/OpenRA.Mods.RA2.dll
engine/bin/OpenRA.Mods.Common.dll'

write_prebuild_evidence()
{
	recorded_git_head=$(git_head)
	: > "$prebuild_evidence"
	for relative_path in $static_input_paths
	do
		absolute_path=$repo_root/$relative_path
		if [ ! -f "$absolute_path" ]; then
			echo "Required impact audit pre-build input is missing: $absolute_path" >&2
			return 1
		fi

		file_hash=$(sha256_file "$absolute_path")
		printf '%s  %s\n' "$file_hash" "$relative_path" >> "$prebuild_evidence"
	done
	prebuild_manifest_hash=$(sha256_file "$prebuild_evidence")
}

verify_prebuild_evidence()
{
	current_git_head=$(git_head)
	if [ "$current_git_head" != "$recorded_git_head" ]; then
		echo "Git HEAD changed after the impact runtime audit build." >&2
		return 1
	fi

	actual_prebuild_manifest_hash=$(sha256_file "$prebuild_evidence")
	if [ "$actual_prebuild_manifest_hash" != "$prebuild_manifest_hash" ]; then
		echo "Impact runtime audit pre-build input manifest changed after it was recorded." >&2
		return 1
	fi

	while read -r expected_hash relative_path
	do
		[ -n "$expected_hash" ] || continue
		if [ ! -f "$repo_root/$relative_path" ]; then
			echo "Evidence hash changed after build: $relative_path" >&2
			return 1
		fi
		actual_hash=$(sha256_file "$repo_root/$relative_path")
		if [ "$actual_hash" != "$expected_hash" ]; then
			echo "Evidence hash changed after build: $relative_path" >&2
			return 1
		fi
	done < "$prebuild_evidence"
}

bind_build_evidence()
{
	: > "$build_evidence"
	while IFS= read -r recorded_input
	do
		printf '%s\n' "$recorded_input" >> "$build_evidence"
	done < "$prebuild_evidence"

	for relative_path in $built_dependency_paths
	do
		absolute_path=$repo_root/$relative_path
		if [ ! -f "$absolute_path" ]; then
			echo "Required impact audit runtime dependency is missing: $absolute_path" >&2
			return 1
		fi

		file_hash=$(sha256_file "$absolute_path")
		printf '%s  %s\n' "$file_hash" "$relative_path" >> "$build_evidence"
	done
	build_manifest_hash=$(sha256_file "$build_evidence")
}

verify_build_evidence()
{
	actual_build_manifest_hash=$(sha256_file "$build_evidence")
	if [ "$actual_build_manifest_hash" != "$build_manifest_hash" ]; then
		echo "Impact runtime audit build evidence changed after it was bound." >&2
		return 1
	fi

	while read -r expected_hash relative_path
	do
		[ -n "$expected_hash" ] || continue
		if [ ! -f "$repo_root/$relative_path" ]; then
			echo "Evidence hash changed after build: $relative_path" >&2
			return 1
		fi
		actual_hash=$(sha256_file "$repo_root/$relative_path")
		if [ "$actual_hash" != "$expected_hash" ]; then
			echo "Evidence hash changed after build: $relative_path" >&2
			return 1
		fi
	done < "$build_evidence"
}

write_final_evidence()
{
	: > "$evidence_manifest_tmp"
	while IFS= read -r recorded_build_evidence
	do
		printf '%s\n' "$recorded_build_evidence" >> "$evidence_manifest_tmp"
	done < "$build_evidence"

	find "$output_root" -type f \
		! -name evidence.sha256 ! -name evidence.json \
		! -name evidence.sha256.tmp ! -name evidence.json.tmp -print |
		LC_ALL=C sort |
		while IFS= read -r output_path
		do
			relative_output=${output_path#"$output_root"/}
			file_hash=$(sha256_file "$output_path")
			printf '%s  %s\n' "$file_hash" "$relative_output"
		done >> "$evidence_manifest_tmp"

	manifest_hash=$(sha256_file "$evidence_manifest_tmp")
	cat > "$evidence_metadata_tmp" <<EOF
{
  "run_id": "$run_id",
  "git_head": "$recorded_git_head",
  "configuration": "Release",
  "build_command": "dotnet build OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj -c Release --no-restore --nologo",
  "evidence_manifest_sha256": "$manifest_hash"
}
EOF
	mv "$evidence_manifest_tmp" "$evidence_manifest"
	mv "$evidence_metadata_tmp" "$evidence_metadata"
}

verify_final_evidence()
{
	while read -r expected_hash relative_path
	do
		[ -n "$expected_hash" ] || continue
		case "$relative_path" in
			packaging/*|OpenRA.Mods.RA2/*|engine/*|mods/*) absolute_path=$repo_root/$relative_path ;;
			*) absolute_path=$output_root/$relative_path ;;
		esac
		actual_hash=$(sha256_file "$absolute_path")
		if [ "$actual_hash" != "$expected_hash" ]; then
			echo "Final evidence hash mismatch: $relative_path" >&2
			return 1
		fi
	done < "$evidence_manifest"

	actual_manifest_hash=$(sha256_file "$evidence_manifest")
	if [ "$actual_manifest_hash" != "$manifest_hash" ]; then
		echo "Impact runtime audit evidence manifest changed after it was recorded." >&2
		return 1
	fi
}

emit_retail_block_from_launch_log()
{
	[ -f "$launch_log" ] || return 1
	if grep -E -i -q 'decode|decoder|invalid|corrupt|fatal|crash' "$launch_log"; then
		return 1
	fi

	file_not_found_exception_pattern='file([[:space:]_-])*not([[:space:]_-])*foundexception'
	if grep -E -i 'exception' "$launch_log" |
		grep -E -i -v "$file_not_found_exception_pattern" |
		grep -q .; then
		return 1
	fi

	retail_asset_pattern='large_clsn|terrorist_explosion|verylarge_clsn|kirovtesla|watersplash|gexp14a|gexpapoa|gexpwasa|conquer\.mix|ra2\.mix|language\.mix'
	missing_semantics_pattern='missing|not([[:space:]]+|-)found|no[[:space:]]+such|unavailable|could[[:space:]]+not[[:space:]]+find'
	resource_label_pattern='file|resource|asset'
	separator_pattern='[[:space:][:punct:]]*'
	retail_block_pattern="(($missing_semantics_pattern)($separator_pattern)(($resource_label_pattern)($separator_pattern))?($retail_asset_pattern)|(($resource_label_pattern)($separator_pattern))?($retail_asset_pattern)($separator_pattern)((is|was)($separator_pattern))?($missing_semantics_pattern)|($file_not_found_exception_pattern).*($retail_asset_pattern)|($retail_asset_pattern).*($file_not_found_exception_pattern))"
	if ! grep -E -i -q "$retail_block_pattern" "$launch_log"; then
		return 1
	fi

	mkdir -p "$runtime_dir/screenshots"
	grep -E -i "$retail_block_pattern" \
		"$launch_log" > "$runtime_dir/blocked-retail-assets.txt" || true
	printf '%s\n' 'runner classified launch failure as BLOCKED_RETAIL_ASSETS' > "$runtime_dir/progress.log"
	printf '{\n  "complete": true,\n  "status": "BLOCKED_RETAIL_ASSETS",\n  "functional_status": "BLOCKED_RETAIL_ASSETS",\n  "performance_status": "NOT_COMPLETED",\n  "total": 16,\n  "completed": 0,\n  "passed": 0,\n  "failed": 0,\n  "screenshots": 0,\n  "burst_complete": false,\n  "burst_impacts": 0,\n  "burst_sprite_schedules": 0,\n  "burst_sprite_added": 0,\n  "burst_sprite_rendered": 0,\n  "burst_sprite_completed": 0,\n  "burst_target_sprite_peak": 0,\n  "burst_global_sprite_peak": 0,\n  "burst_cleanup_ticks": 0,\n  "sample_count": 0,\n  "performance_sample_count": 0,\n  "performance_p95": {"render": 0, "world_tick": 0, "render_prepare": 0, "render_flip": 0, "batches": 0},\n  "performance_max": {"render": 0, "world_tick": 0, "render_prepare": 0, "render_flip": 0, "batches": 0},\n  "audio_backend_evidence": "not_started",\n  "audio_backend_limitation": "runtime audit did not start",\n  "detail": "runner classified launch failure as missing retail assets"\n}\n' > "$summary"
	return 0
}

trap 'cleanup' EXIT
trap 'exit 130' HUP INT TERM

if ! mkdir "$lock_dir" 2>/dev/null; then
	echo "Another build/runtime audit owns the repository runtime audit lock: $lock_dir" >&2
	exit 2
fi
lock_owned=true
lock_identity=$(directory_identity "$lock_dir") || {
	echo "Unable to record the repository runtime audit lock identity: $lock_dir" >&2
	exit 2
}

mkdir -p "$output_root"

dotnet_bin=${OPENRA_IMPACT_EFFECT_AUDIT_DOTNET:-}
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

python_bin=${OPENRA_IMPACT_EFFECT_AUDIT_PYTHON:-}
if [ -z "$python_bin" ]; then
	if command -v python3 >/dev/null 2>&1; then
		python_bin=$(command -v python3)
	else
		echo "Python 3 is required for strict impact audit JSON validation." >&2
		exit 2
	fi
fi

if [ ! -x "$python_bin" ] ||
	! "$python_bin" -c 'import json, sys; raise SystemExit(0 if sys.version_info.major == 3 else 1)' >/dev/null 2>&1; then
	echo "Python 3 JSON validator is not executable or compatible: $python_bin" >&2
	exit 2
fi

write_prebuild_evidence

echo "Building the scoped RA2 impact runtime audit assembly in Release mode..."
DOTNET_ROLL_FORWARD=LatestMajor "$dotnet_bin" build \
	"$repo_root/OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj" \
	-c Release --no-restore --nologo

if [ ! -f "$repo_root/engine/bin/OpenRA.dll" ]; then
	echo "Built OpenRA engine not found after the scoped build." >&2
	exit 2
fi

verify_prebuild_evidence
bind_build_evidence
verify_build_evidence

cd "$repo_root/engine"
OPENRA_IMPACT_EFFECT_AUDIT=true \
OPENRA_V3_RUNTIME_AUDIT=false \
OPENRA_IOS_DESTRUCTION_AUDIT=false \
OPENRA_IOS_PERF_COUNT= \
OPENRA_IMPACT_EFFECT_AUDIT_TIMEOUT_TICKS=$timeout_ticks \
OPENRA_IMPACT_EFFECT_AUDIT_RUN_ID=$run_id \
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
launch_identity_pending=true

capture_status=0
capture_initial_game_identity || capture_status=$?
case "$capture_status" in
	0) ;;
	1)
		echo "Unable to capture the expected dotnet/OpenRA/LaunchInto identity for PID $game_pid." >&2
		exit 1
		;;
	2)
		game_pid=
		launch_identity_pending=false
		;;
esac

summary_complete=false
while [ "$(date +%s)" -lt "$deadline" ]
do
	if [ -f "$summary" ]; then
		probe_status=0
		strict_summary_json probe "$summary" >/dev/null || probe_status=$?
		case "$probe_status" in
			0)
				summary_complete=true
				break
				;;
			1) ;;
			*) exit 1 ;;
		esac
	fi

	if [ -z "$game_pid" ] || ! game_process_is_running; then
		if emit_retail_block_from_launch_log; then
			summary_complete=true
			break
		fi

		echo "OpenRA exited before the impact runtime audit completed. See $launch_log" >&2
		exit 1
	fi

	sleep 1
done

if [ "$summary_complete" != true ]; then
	if ! emit_retail_block_from_launch_log; then
		echo "Impact runtime audit did not complete within ${run_timeout_seconds}s: $run_id" >&2
		exit 1
	fi
fi

stop_game
game_pid=
verify_prebuild_evidence
verify_build_evidence
copy_report_once

summary_status=
if ! summary_status=$(strict_summary_json validate "$staged_summary"); then
	echo "Impact runtime audit completed without the required 16/16 plus burst evidence or valid JSON: $staged_summary" >&2
	exit 1
fi

case "$summary_status" in
	BLOCKED_RETAIL_ASSETS)
		publish_staged_report
		write_final_evidence
		verify_final_evidence
		echo "Impact runtime audit blocked by player-owned retail assets: $output_root" >&2
		exit 3
		;;
	PASSED)
		validate_staged_success_images
		publish_staged_report
		write_final_evidence
		verify_final_evidence
		;;
	FAILED)
		echo "Impact runtime audit completed with FAILED status: $staged_summary" >&2
		exit 1
		;;
esac

echo "Impact runtime audit report: $output_root"
