#!/bin/sh
set -eu

if [ "${1:-}" = "build" ]; then
	if [ -n "${FAKE_IMPACT_AUDIT_BUILD_LOG:-}" ]; then
		printf '%s\n' "$*" >> "$FAKE_IMPACT_AUDIT_BUILD_LOG"
	fi
	if [ -n "${FAKE_IMPACT_AUDIT_BUILD_MUTATE_FILE:-}" ]; then
		printf 'changed-during-build\n' >> "$FAKE_IMPACT_AUDIT_BUILD_MUTATE_FILE"
	fi
	if [ -n "${FAKE_IMPACT_AUDIT_REPLACE_LOCK_DIR:-}" ]; then
		mv "$FAKE_IMPACT_AUDIT_REPLACE_LOCK_DIR" "$FAKE_IMPACT_AUDIT_REPLACE_LOCK_DIR.original"
		mkdir "$FAKE_IMPACT_AUDIT_REPLACE_LOCK_DIR"
	fi
	if [ -n "${FAKE_IMPACT_AUDIT_SIGNAL_PARENT:-}" ]; then
		kill -"$FAKE_IMPACT_AUDIT_SIGNAL_PARENT" "$PPID"
		sleep 1
	fi

	exit "${FAKE_IMPACT_AUDIT_BUILD_EXIT_CODE:-0}"
fi

: "${FAKE_IMPACT_AUDIT_PID_FILE:?}"
: "${OPENRA_IMPACT_EFFECT_AUDIT_SUPPORT_DIR:?}"
: "${OPENRA_IMPACT_EFFECT_AUDIT_RUN_ID:?}"

printf '%s\n' "$$" > "$FAKE_IMPACT_AUDIT_PID_FILE"
if [ -n "${FAKE_IMPACT_AUDIT_RUNNER_PID_FILE:-}" ]; then
	printf '%s\n' "$PPID" > "$FAKE_IMPACT_AUDIT_RUNNER_PID_FILE"
fi
runtime_dir=$OPENRA_IMPACT_EFFECT_AUDIT_SUPPORT_DIR/Logs/ImpactEffectsRuntimeAudit/$OPENRA_IMPACT_EFFECT_AUDIT_RUN_ID
mkdir -p "$runtime_dir/baselines" "$runtime_dir/screenshots"
if [ -n "${FAKE_IMPACT_AUDIT_MUTATE_FILE:-}" ]; then
	printf 'changed-after-evidence\n' >> "$FAKE_IMPACT_AUDIT_MUTATE_FILE"
fi

case "${FAKE_IMPACT_AUDIT_RESULT:-failed}" in
	missing-exit)
		sleep 1
		printf '%s\n' 'Launch error: could not find gexp14a.wav; missing retail asset.' >&2
		exit 41
		;;
	decode-exit)
		sleep 1
		printf '%s\n' 'Launch error: failed to decode gexp14a.wav due to invalid audio data.' >&2
		exit 42
		;;
	decoder-find-exit)
		sleep 1
		printf '%s\n' 'Launch error: could not find decoder for gexp14a.wav.' >&2
		exit 43
		;;
	missing-warning-fatal-exit)
		sleep 1
		printf '%s\n' 'Warning: missing resource gexp14a.wav.' >&2
		printf '%s\n' 'Fatal exception: audio subsystem initialization failed.' >&2
		exit 44
		;;
	file-not-found-exception-exit)
		sleep 1
		printf '%s\n' 'System.IO.FileNotFoundException: retail resource gexp14a.wav was not found.' >&2
		exit 45
		;;
	passed)
		printf 'case,result\nimpact,PASSED\n' > "$runtime_dir/cases.csv"
		printf 'scope,case,sequence\ncase,impact,nc_core_small\n' > "$runtime_dir/effects.csv"
		printf 'kind,tick,render\nsample,1,1\np95,-1,1\nmax,-1,1\n' > "$runtime_dir/performance.csv"
		printf '{"weapons":4}\n' > "$runtime_dir/resolved-weapons.json"
		printf 'complete\n' > "$runtime_dir/progress.log"
		i=1
		while [ "$i" -le "${FAKE_IMPACT_AUDIT_BASELINE_COUNT:-16}" ]
		do
			printf 'baseline-png-%s\n' "$i" > "$runtime_dir/baselines/impact-$(printf '%02d' "$i").png"
			i=$((i + 1))
		done
		i=1
		while [ "$i" -le "${FAKE_IMPACT_AUDIT_SCREENSHOT_COUNT:-16}" ]
		do
			printf 'png-%s\n' "$i" > "$runtime_dir/screenshots/impact-$(printf '%02d' "$i").png"
			i=$((i + 1))
		done
		summary_variant=${FAKE_IMPACT_AUDIT_SUMMARY_VARIANT:-valid}
		if [ "${FAKE_IMPACT_AUDIT_OMIT_FINAL_PROTOCOL:-false}" = true ]; then
			summary_variant=legacy
		fi
		case "$summary_variant" in
			malformed)
				printf '{"complete": true, "status": "PASSED",\n' > "$runtime_dir/summary.json"
				;;
			legacy)
				printf '{\n  "complete": true,\n  "status": "PASSED",\n  "total": 16,\n  "completed": 16,\n  "passed": 16,\n  "failed": 0,\n  "screenshots": 16,\n  "burst_complete": true,\n  "burst_impacts": 16,\n  "burst_sprite_schedules": 64\n}\n' > "$runtime_dir/summary.json"
				;;
			*)
				duplicate_line=
				sample_count=3
				metric_render=1.25
				include_metrics=true
				case "$summary_variant" in
					valid) ;;
					duplicate) duplicate_line='  "status": "FAILED",' ;;
					wrong-type) sample_count=true ;;
					zero-samples) sample_count=0 ;;
					missing-metrics) include_metrics=false ;;
					nonfinite) metric_render=NaN ;;
					*) exit 67 ;;
				esac
				{
					printf '{\n  "complete": true,\n  "status": "PASSED",\n'
					[ -z "$duplicate_line" ] || printf '%s\n' "$duplicate_line"
					printf '  "functional_status": "PASSED",\n  "performance_status": "RECORDED_FOR_REVIEW",\n'
					printf '  "total": 16,\n  "completed": 16,\n  "passed": 16,\n  "failed": 0,\n  "screenshots": 16,\n'
					printf '  "burst_complete": true,\n  "burst_impacts": 16,\n  "burst_sprite_schedules": 64,\n'
					printf '  "burst_sprite_added": 64,\n  "burst_sprite_rendered": 64,\n  "burst_sprite_completed": 64,\n'
					printf '  "burst_target_sprite_peak": 16,\n  "burst_global_sprite_peak": 16,\n  "burst_cleanup_ticks": 5,\n'
					printf '  "sample_count": %s,\n  "performance_sample_count": 3,\n' "$sample_count"
					if [ "$include_metrics" = true ]; then
						printf '  "performance_p95": {"render": %s, "world_tick": 1, "render_prepare": 1, "render_flip": 1, "batches": 1},\n' "$metric_render"
						printf '  "performance_max": {"render": 2, "world_tick": 2, "render_prepare": 2, "render_flip": 2, "batches": 2},\n'
					fi
					printf '  "audio_backend_evidence": "decoded_asset_plus_matching_ISound_SeekPosition_progress",\n'
					printf '  "audio_backend_limitation": "ISound exposes no per-handle backend error API",\n  "detail": ""\n}\n'
				} > "$runtime_dir/summary.json"
				;;
		esac
		;;
	blocked)
		printf 'gexp14a.wav\n' > "$runtime_dir/blocked-retail-assets.txt"
		printf 'blocked\n' > "$runtime_dir/progress.log"
		printf '{\n  "complete": true,\n  "status": "BLOCKED_RETAIL_ASSETS",\n  "functional_status": "BLOCKED_RETAIL_ASSETS",\n  "performance_status": "NOT_COMPLETED",\n  "total": 16,\n  "completed": 0,\n  "passed": 0,\n  "failed": 0,\n  "screenshots": 0,\n  "burst_complete": false,\n  "burst_impacts": 0,\n  "burst_sprite_schedules": 0,\n  "burst_sprite_added": 0,\n  "burst_sprite_rendered": 0,\n  "burst_sprite_completed": 0,\n  "burst_target_sprite_peak": 0,\n  "burst_global_sprite_peak": 0,\n  "burst_cleanup_ticks": 0,\n  "sample_count": 0,\n  "performance_sample_count": 0,\n  "performance_p95": {"render": 0, "world_tick": 0, "render_prepare": 0, "render_flip": 0, "batches": 0},\n  "performance_max": {"render": 0, "world_tick": 0, "render_prepare": 0, "render_flip": 0, "batches": 0},\n  "audio_backend_evidence": "not_started",\n  "audio_backend_limitation": "runtime audit did not start",\n  "detail": "blocked by retail assets"\n}\n' > "$runtime_dir/summary.json"
		;;
	hang)
		printf 'running\n' > "$runtime_dir/progress.log"
		;;
	*)
		printf 'failed\n' > "$runtime_dir/progress.log"
		printf '{\n  "complete": true,\n  "status": "FAILED",\n  "total": 16,\n  "completed": 16,\n  "passed": 15,\n  "failed": 1,\n  "screenshots": 15,\n  "burst_complete": false\n}\n' > "$runtime_dir/summary.json"
		;;
esac

handle_term()
{
	if [ -n "${FAKE_IMPACT_AUDIT_IDENTITY_CHANGE_ON_TERM:-}" ] &&
		[ -n "${FAKE_IMPACT_AUDIT_PS_STATE_FILE:-}" ]; then
		printf '%s\n' "$FAKE_IMPACT_AUDIT_IDENTITY_CHANGE_ON_TERM" > "$FAKE_IMPACT_AUDIT_PS_STATE_FILE"
	fi

	case "${FAKE_IMPACT_AUDIT_TERM_BEHAVIOR:-ignore}" in
		exit) exit 0 ;;
		ignore) return 0 ;;
		*) exit 65 ;;
	esac
}

trap 'handle_term' TERM
while :
do
	sleep 1
done
