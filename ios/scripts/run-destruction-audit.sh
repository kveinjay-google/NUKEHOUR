#!/bin/sh
set -eu

device_id="${OPENRA_IOS_DEVICE_ID:-C5CA8947-3880-593B-84D6-B57DE642A8FD}"
bundle_id="${OPENRA_IOS_BUNDLE_ID:-com.openra.ipad.personal}"
map="${OPENRA_IOS_DESTRUCTION_MAP:-south-pacific}"
iterations="${OPENRA_IOS_DESTRUCTION_ITERATIONS:-3}"
settle_ticks="${OPENRA_IOS_DESTRUCTION_SETTLE_TICKS:-40}"
timeout_ticks="${OPENRA_IOS_DESTRUCTION_TIMEOUT_TICKS:-250}"
batch_size="${OPENRA_IOS_DESTRUCTION_BATCH_SIZE:-20}"
actor_filter="${OPENRA_IOS_DESTRUCTION_ACTOR_FILTER:-}"
refinement="${OPENRA_IOS_DESTRUCTION_REFINEMENT:-true}"
stale_seconds="${OPENRA_IOS_DESTRUCTION_STALE_SECONDS:-30}"
run_id="${OPENRA_IOS_DESTRUCTION_RUN_ID:-$(date -u +%Y%m%dT%H%M%SZ)}"
run_timeout_seconds="${OPENRA_IOS_DESTRUCTION_RUN_TIMEOUT_SECONDS:-7200}"
output_root="${OPENRA_IOS_DESTRUCTION_OUTPUT:-$(pwd)/docs/testing/results/destruction-audit-$run_id}"
summary="$output_root/summary.json"

mkdir -p "$output_root"

xcrun devicectl device process launch \
	--device "$device_id" \
	--terminate-existing \
	--timeout 30 \
	--environment-variables "{\"OPENRA_IOS_AUTOTEST_SKIRMISH\":\"$map\",\"OPENRA_IOS_DESTRUCTION_AUDIT\":\"true\",\"OPENRA_IOS_DESTRUCTION_ITERATIONS\":\"$iterations\",\"OPENRA_IOS_DESTRUCTION_SETTLE_TICKS\":\"$settle_ticks\",\"OPENRA_IOS_DESTRUCTION_TIMEOUT_TICKS\":\"$timeout_ticks\",\"OPENRA_IOS_DESTRUCTION_BATCH_SIZE\":\"$batch_size\",\"OPENRA_IOS_DESTRUCTION_ACTOR_FILTER\":\"$actor_filter\",\"OPENRA_IOS_DESTRUCTION_RUN_ID\":\"$run_id\"}" \
	"$bundle_id" >"$output_root/launch.log" 2>&1

deadline=$(($(date +%s) + run_timeout_seconds))
complete=false
stalled=false
last_signature=""
last_progress_epoch="$(date +%s)"
while [ "$(date +%s)" -lt "$deadline" ]; do
	rm -f "$summary"
	if xcrun devicectl device copy from \
		--device "$device_id" \
		--domain-type appDataContainer \
		--domain-identifier "$bundle_id" \
		--source "Documents/OpenRA/Logs/DestructionAudit/$run_id/summary.json" \
		--destination "$summary" \
		--timeout 15 >"$output_root/copy-summary.log" 2>&1; then
		if grep -q '"complete": true' "$summary"; then
			complete=true
			break
		fi

		signature="$(cksum "$summary" | awk '{ print $1 ":" $2 }')"
		if [ "$signature" != "$last_signature" ]; then
			last_signature="$signature"
			last_progress_epoch="$(date +%s)"
		elif [ "$(($(date +%s) - last_progress_epoch))" -ge "$stale_seconds" ]; then
			stalled=true
			break
		fi
	fi

	sleep 2
done

xcrun devicectl device copy from \
	--device "$device_id" \
	--domain-type appDataContainer \
	--domain-identifier "$bundle_id" \
	--source "Documents/OpenRA/Logs/DestructionAudit/$run_id" \
	--destination "$output_root/report" \
	--timeout 60 >"$output_root/copy-report.log" 2>&1 || true

if [ "$complete" != true ]; then
	if [ "$stalled" = true ] && [ "$refinement" = true ] && [ -z "$actor_filter" ]; then
		progress="$output_root/report/progress.log"
		members=""
		if [ -s "$progress" ]; then
			members="$(awk -F'members=' '/batch-start/{ value=$2 } END{ print value }' "$progress")"
		fi

		if [ -n "$members" ]; then
			refinement_root="$output_root/refinement"
			mkdir -p "$refinement_root"
			refinement_failed=false
			for actor in $(echo "$members" | tr ',' ' '); do
				if ! OPENRA_IOS_DEVICE_ID="$device_id" \
					OPENRA_IOS_BUNDLE_ID="$bundle_id" \
					OPENRA_IOS_DESTRUCTION_MAP="$map" \
					OPENRA_IOS_DESTRUCTION_ITERATIONS="$iterations" \
					OPENRA_IOS_DESTRUCTION_SETTLE_TICKS="$settle_ticks" \
					OPENRA_IOS_DESTRUCTION_TIMEOUT_TICKS="$timeout_ticks" \
					OPENRA_IOS_DESTRUCTION_BATCH_SIZE=1 \
					OPENRA_IOS_DESTRUCTION_ACTOR_FILTER="$actor" \
					OPENRA_IOS_DESTRUCTION_REFINEMENT=false \
					OPENRA_IOS_DESTRUCTION_STALE_SECONDS="$stale_seconds" \
					OPENRA_IOS_DESTRUCTION_RUN_TIMEOUT_SECONDS="$run_timeout_seconds" \
					OPENRA_IOS_DESTRUCTION_RUN_ID="${run_id}-refine-${actor}" \
					OPENRA_IOS_DESTRUCTION_OUTPUT="$refinement_root/$actor" \
					"$0"; then
					refinement_failed=true
				fi
			done

			echo "Destruction audit stalled; individual refinement reports: $refinement_root" >&2
			[ "$refinement_failed" = false ] || exit 1
			exit 1
		fi
	fi

	echo "Destruction audit did not complete before timeout or stale-progress limit: $run_id" >&2
	exit 1
fi

test -s "$summary"
if ! grep -q '"failed": 0' "$summary"; then
	echo "Destruction audit completed with failures: $summary" >&2
	exit 1
fi

if ! grep -q '"skipped": 0' "$summary"; then
	echo "Destruction audit did not cover every actor on this map: $summary" >&2
	exit 1
fi

echo "Destruction audit report: $output_root"
