#!/bin/sh
set -eu

device_id="${OPENRA_IOS_DEVICE_ID:-C5CA8947-3880-593B-84D6-B57DE642A8FD}"
bundle_id="${OPENRA_IOS_BUNDLE_ID:-com.openra.ipad.personal}"
map="${OPENRA_IOS_SMOKE_MAP:-pinch-point}"
timeout_seconds="${OPENRA_IOS_SMOKE_TIMEOUT_SECONDS:-120}"
launch_nonce="$(uuidgen | tr -d '-' | tr '[:upper:]' '[:lower:]')"
run_id="${OPENRA_IOS_SMOKE_RUN_ID:-$(date -u +%Y%m%dT%H%M%SZ)-$launch_nonce}"
output_root="${OPENRA_IOS_SMOKE_OUTPUT:-$(pwd)/docs/testing/results/loading-smoke-$run_id}"
start_needle="smoke-start run=$run_id launch=$launch_nonce"
failure_needle="smoke-failure run=$run_id launch=$launch_nonce "
ready_needle="smoke-ready run=$run_id launch=$launch_nonce criterion=duration-and-progress"

mkdir -p "$output_root"

xcrun devicectl device process launch \
	--device "$device_id" \
	--terminate-existing \
	--timeout 30 \
	--environment-variables "{\"OPENRA_IOS_AUTOTEST_SKIRMISH\":\"$map\",\"OPENRA_IOS_SMOKE_RUN_ID\":\"$run_id\",\"OPENRA_IOS_SMOKE_LAUNCH_NONCE\":\"$launch_nonce\"}" \
	"$bundle_id" >"$output_root/launch.log" 2>&1

deadline=$(($(date +%s) + timeout_seconds))
attempt=0
ready=false
failure=false
start_seen=false
while [ "$(date +%s)" -lt "$deadline" ]; do
	attempt=$((attempt + 1))
	probe="$output_root/ios-load-stage-$attempt.log"
	if xcrun devicectl device copy from \
		--device "$device_id" \
		--domain-type appDataContainer \
		--domain-identifier "$bundle_id" \
		--source "Documents/OpenRA/Logs/ios-load-stage.log" \
		--destination "$probe" \
		--timeout 15 >"$output_root/copy-stage-$attempt.log" 2>&1; then
		if rg -F -q "$start_needle" "$probe"; then
			start_seen=true
		fi
		if [ "$start_seen" = true ] && rg -F -q "$failure_needle" "$probe"; then
			failure=true
			break
		fi
		if [ "$start_seen" = true ] && rg -F -q "$ready_needle" "$probe"; then
			ready=true
			break
		fi
	fi

	sleep 2
done

if [ "$ready" != true ]; then
	xcrun devicectl device copy from \
		--device "$device_id" \
		--domain-type appDataContainer \
		--domain-identifier "$bundle_id" \
		--source "Documents/OpenRA/Logs" \
		--destination "$output_root/failure-logs" \
		--timeout 60 >"$output_root/copy-failure-logs.log" 2>&1 || true
	xcrun devicectl device copy from \
		--device "$device_id" \
		--domain-type appDataContainer \
		--domain-identifier "$bundle_id" \
		--source "Documents/OpenRA/Diagnostics" \
		--destination "$output_root/failure-diagnostics" \
		--timeout 60 >"$output_root/copy-failure-diagnostics.log" 2>&1 || true
	if [ "$failure" = true ]; then
		rg -F "$failure_needle" "$probe" >&2 || true
	fi
	echo "Loading smoke failed before sustained responsive gameplay: $run_id" >&2
	exit 1
fi

echo "Loading smoke reached sustained responsive gameplay: $run_id"
echo "Evidence: $probe"
