#!/bin/sh
set -eu

device_id="${OPENRA_IOS_DEVICE_ID:-C5CA8947-3880-593B-84D6-B57DE642A8FD}"
bundle_id="${OPENRA_IOS_BUNDLE_ID:-com.openra.ipad.personal}"
map="${OPENRA_IOS_BENCHMARK_MAP:-pinch-point}"
counts="${OPENRA_IOS_BENCHMARK_COUNTS:-100 300 600 1000}"
states="${OPENRA_IOS_BENCHMARK_STATES:-idle move combat}"
cameras="${OPENRA_IOS_BENCHMARK_CAMERAS:-visible empty}"
pause_modes="${OPENRA_IOS_BENCHMARK_PAUSE_MODES:-false true}"
warmup_seconds="${OPENRA_IOS_BENCHMARK_WARMUP_SECONDS:-5}"
duration_seconds="${OPENRA_IOS_BENCHMARK_DURATION_SECONDS:-30}"
output_root="${OPENRA_IOS_BENCHMARK_OUTPUT:-$(pwd)/docs/performance/results/$(date -u +%Y%m%dT%H%M%SZ)}"
run_id="${OPENRA_IOS_BENCHMARK_RUN_ID:-$(date -u +%Y%m%dT%H%M%SZ)}"
runtime="${OPENRA_IOS_BENCHMARK_RUNTIME:-unspecified}"
render_scale="${OPENRA_IOS_BENCHMARK_RENDER_SCALE:-1.0}"

mkdir -p "$output_root"

run_case() {
	count="$1"
	state="$2"
	camera="$3"
	paused="$4"
	prefix="${run_id}-units-${count}-${state}-${camera}-paused-${paused}"
	destination="$output_root/$prefix"
	mkdir -p "$destination"

	echo "Running $prefix"
	xcrun devicectl device process launch \
		--device "$device_id" \
		--terminate-existing \
		--timeout 30 \
		--environment-variables "{\"OPENRA_IOS_AUTOTEST_SKIRMISH\":\"$map\",\"OPENRA_IOS_PERF_PREFIX\":\"$prefix\",\"OPENRA_IOS_PERF_COUNT\":\"$count\",\"OPENRA_IOS_PERF_STATE\":\"$state\",\"OPENRA_IOS_PERF_CAMERA\":\"$camera\",\"OPENRA_IOS_PERF_PAUSED\":\"$paused\",\"OPENRA_IOS_PERF_UNIT\":\"e1\",\"OPENRA_IOS_PERF_RUNTIME\":\"$runtime\",\"OPENRA_IOS_PERF_RENDER_SCALE\":\"$render_scale\",\"OPENRA_BENCHMARK_WARMUP_SECONDS\":\"$warmup_seconds\",\"OPENRA_BENCHMARK_DURATION_SECONDS\":\"$duration_seconds\"}" \
		"$bundle_id" >"$destination/launch.log" 2>&1

	deadline=$(($(date +%s) + warmup_seconds + duration_seconds + 120))
	while [ "$(date +%s)" -lt "$deadline" ]; do
		if xcrun devicectl device copy from \
			--device "$device_id" \
			--domain-type appDataContainer \
			--domain-identifier "$bundle_id" \
			--source "Documents/OpenRA/Logs/Performance/$prefix/summary.json" \
			--destination "$destination/summary.json" \
			--timeout 15 >"$destination/copy.log" 2>&1; then
			break
		fi

		sleep 2
	done

	test -s "$destination/summary.json"
}

for count in $counts; do
	for state in $states; do
		for camera in $cameras; do
			for paused in $pause_modes; do
				run_case "$count" "$state" "$camera" "$paused"
			done
		done
	done
done

echo "Benchmark reports: $output_root"
