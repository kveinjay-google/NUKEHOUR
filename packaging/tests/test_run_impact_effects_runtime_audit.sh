#!/bin/sh
set -eu

repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
runner=$repo_root/packaging/run_impact_effects_runtime_audit.sh
audit_source=$repo_root/OpenRA.Mods.RA2/Traits/ImpactEffectsRuntimeAudit.cs
effect_source=$repo_root/engine/OpenRA.Mods.Common/Warheads/CreateEffectWarhead.cs
benchmark_source=$repo_root/OpenRA.Mods.RA2/Traits/IosHighUnitBenchmark.cs
fake_dotnet=$repo_root/packaging/tests/fixtures/impact-audit-fake-dotnet.sh
failing_bin=$repo_root/packaging/tests/fixtures/failing-bin
controlled_bin=$repo_root/packaging/tests/fixtures/controlled-bin
impact_controlled_bin=$repo_root/packaging/tests/fixtures/impact-controlled-bin
test_case=${1:-all}

fail()
{
	printf 'FAIL: %s\n' "$1" >&2
	return 1
}

wait_for_file()
{
	path=$1
	remaining=$2
	while [ "$remaining" -gt 0 ]
	do
		[ -s "$path" ] && return 0
		sleep 1
		remaining=$((remaining - 1))
	done

	return 1
}

wait_for_exit()
{
	pid=$1
	remaining=$2
	while [ "$remaining" -gt 0 ]
	do
		if ! kill -0 "$pid" 2>/dev/null; then
			wait "$pid" 2>/dev/null || true
			return 0
		fi

		sleep 1
		remaining=$((remaining - 1))
	done

	return 1
}

kill_test_process()
{
	pid=$1
	expected=$2
	case "$pid" in
		''|*[!0-9]*) return 0 ;;
	esac

	command=$(ps -p "$pid" -o command= 2>/dev/null || true)
	case "$command" in
		*"$expected"*) kill -KILL "$pid" 2>/dev/null || true ;;
	esac
}

run_runner_at()
{
	test_root=$1
	run_id=$2
	path_prefix=$3
	runner_path=$4
	audit_dotnet=${IMPACT_TEST_DOTNET_OVERRIDE:-$fake_dotnet}
	audit_python=${IMPACT_TEST_PYTHON_OVERRIDE:-$(command -v python3)}
	mkdir -p "$test_root/support"
	PATH="$path_prefix$impact_controlled_bin:$controlled_bin:$PATH" \
	OPENRA_IMPACT_EFFECT_AUDIT_DOTNET="$audit_dotnet" \
	OPENRA_IMPACT_EFFECT_AUDIT_PYTHON="$audit_python" \
	OPENRA_IMPACT_EFFECT_AUDIT_SUPPORT_DIR="$test_root/support" \
	OPENRA_IMPACT_EFFECT_AUDIT_OUTPUT="$test_root/output" \
	OPENRA_IMPACT_EFFECT_AUDIT_RUN_ID="$run_id" \
	OPENRA_IMPACT_EFFECT_AUDIT_RUN_TIMEOUT_SECONDS=10 \
	OPENRA_IMPACT_EFFECT_AUDIT_TERM_GRACE_SECONDS=1 \
	OPENRA_IMPACT_EFFECT_AUDIT_REAP_GRACE_SECONDS=1 \
	FAKE_IMPACT_AUDIT_PID_FILE="$test_root/game.pid" \
	FAKE_IMPACT_AUDIT_BUILD_LOG="${FAKE_IMPACT_AUDIT_BUILD_LOG:-}" \
	FAKE_IMPACT_AUDIT_BUILD_EXIT_CODE="${FAKE_IMPACT_AUDIT_BUILD_EXIT_CODE:-0}" \
	FAKE_IMPACT_AUDIT_BUILD_MUTATE_FILE="${FAKE_IMPACT_AUDIT_BUILD_MUTATE_FILE:-}" \
	FAKE_IMPACT_AUDIT_REPLACE_LOCK_DIR="${FAKE_IMPACT_AUDIT_REPLACE_LOCK_DIR:-}" \
	FAKE_IMPACT_AUDIT_SIGNAL_PARENT="${FAKE_IMPACT_AUDIT_SIGNAL_PARENT:-}" \
	FAKE_IMPACT_AUDIT_MUTATE_FILE="${FAKE_IMPACT_AUDIT_MUTATE_FILE:-}" \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR="${FAKE_IMPACT_AUDIT_TERM_BEHAVIOR:-ignore}" \
	FAKE_IMPACT_AUDIT_IDENTITY_CHANGE_ON_TERM="${FAKE_IMPACT_AUDIT_IDENTITY_CHANGE_ON_TERM:-}" \
	FAKE_IMPACT_AUDIT_PS_STATE_FILE="${FAKE_IMPACT_AUDIT_PS_STATE_FILE:-}" \
	FAKE_IMPACT_AUDIT_PS_CAPTURE_LOG="${FAKE_IMPACT_AUDIT_PS_CAPTURE_LOG:-}" \
	FAKE_IMPACT_AUDIT_PS_IDENTITY_CALL_COUNT_FILE="${FAKE_IMPACT_AUDIT_PS_IDENTITY_CALL_COUNT_FILE:-}" \
	FAKE_IMPACT_AUDIT_PS_TRANSIENT_COUNT="${FAKE_IMPACT_AUDIT_PS_TRANSIENT_COUNT:-0}" \
	FAKE_IMPACT_AUDIT_PS_CHANGED_START_AFTER="${FAKE_IMPACT_AUDIT_PS_CHANGED_START_AFTER:-0}" \
	FAKE_IMPACT_AUDIT_PS_DOTNET="$audit_dotnet" \
	FAKE_IMPACT_AUDIT_RUNNER_PID_FILE="${FAKE_IMPACT_AUDIT_RUNNER_PID_FILE:-}" \
	FAKE_IMPACT_AUDIT_CP_COUNT_FILE="${FAKE_IMPACT_AUDIT_CP_COUNT_FILE:-}" \
	FAKE_IMPACT_AUDIT_CP_LOG_FILE="${FAKE_IMPACT_AUDIT_CP_LOG_FILE:-}" \
	FAKE_IMPACT_AUDIT_CP_MUTATE_FILE="${FAKE_IMPACT_AUDIT_CP_MUTATE_FILE:-}" \
	FAKE_IMPACT_AUDIT_CP_MUTATE_ON="${FAKE_IMPACT_AUDIT_CP_MUTATE_ON:-0}" \
	FAKE_IMPACT_AUDIT_MV_LOG_FILE="${FAKE_IMPACT_AUDIT_MV_LOG_FILE:-}" \
	FAKE_IMPACT_AUDIT_BASELINE_COUNT="${FAKE_IMPACT_AUDIT_BASELINE_COUNT:-16}" \
	FAKE_IMPACT_AUDIT_SCREENSHOT_COUNT="${FAKE_IMPACT_AUDIT_SCREENSHOT_COUNT:-16}" \
	FAKE_IMPACT_AUDIT_OMIT_FINAL_PROTOCOL="${FAKE_IMPACT_AUDIT_OMIT_FINAL_PROTOCOL:-false}" \
	FAKE_IMPACT_AUDIT_SUMMARY_VARIANT="${FAKE_IMPACT_AUDIT_SUMMARY_VARIANT:-valid}" \
	FAKE_IMPACT_AUDIT_RESULT="${FAKE_IMPACT_AUDIT_RESULT:-failed}" \
	"$runner_path"
}

assert_lock_absent()
{
	lock_dir=$1
	message=$2
	[ ! -e "$lock_dir" ] || fail "$message"
}

prepare_fake_repo()
{
	test_root=$1
	fake_repo=$test_root/repo
	mkdir -p \
		"$fake_repo/packaging" \
		"$fake_repo/OpenRA.Mods.RA2/Traits" \
		"$fake_repo/engine/OpenRA.Mods.Common/Warheads" \
		"$fake_repo/mods/ra2/weapons" \
		"$fake_repo/mods/ra2/sequences" \
		"$fake_repo/mods/ra2/bits/animations" \
		"$fake_repo/engine/bin"
	cp "$runner" "$fake_repo/packaging/run_impact_effects_runtime_audit.sh"
	printf '<Project />\n' > "$fake_repo/OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj"
	printf 'impact audit source\n' > "$fake_repo/OpenRA.Mods.RA2/Traits/ImpactEffectsRuntimeAudit.cs"
	printf 'benchmark dispatch\n' > "$fake_repo/OpenRA.Mods.RA2/Traits/IosHighUnitBenchmark.cs"
	printf 'effect diagnostic source\n' > "$fake_repo/engine/OpenRA.Mods.Common/Warheads/CreateEffectWarhead.cs"
	printf 'explosion rules\n' > "$fake_repo/mods/ra2/weapons/explosions.yaml"
	printf 'misc rules\n' > "$fake_repo/mods/ra2/weapons/misc.yaml"
	printf 'sequence rules\n' > "$fake_repo/mods/ra2/sequences/misc.yaml"
	printf 'core\n' > "$fake_repo/mods/ra2/bits/animations/nc-impact-core.png"
	printf 'ring\n' > "$fake_repo/mods/ra2/bits/animations/nc-impact-ring.png"
	printf 'debris\n' > "$fake_repo/mods/ra2/bits/animations/nc-impact-debris.png"
	printf 'engine\n' > "$fake_repo/engine/bin/OpenRA.dll"
	printf 'game assembly\n' > "$fake_repo/engine/bin/OpenRA.Game.dll"
	printf 'platform assembly\n' > "$fake_repo/engine/bin/OpenRA.Platforms.Default.dll"
	printf 'ra2 assembly\n' > "$fake_repo/engine/bin/OpenRA.Mods.RA2.dll"
	printf 'common assembly\n' > "$fake_repo/engine/bin/OpenRA.Mods.Common.dll"
}

prepare_counting_cp()
{
	test_root=$1
	fixture_bin=$test_root/cp-bin
	mkdir -p "$fixture_bin"
	printf '%s\n' \
		'#!/bin/sh' \
		'set -eu' \
		'if [ -n "${FAKE_IMPACT_AUDIT_CP_LOG_FILE:-}" ]; then' \
		'  printf "%s\\n" "$*" >> "$FAKE_IMPACT_AUDIT_CP_LOG_FILE"' \
		'fi' \
		'case "${1:-}" in' \
		'  */Logs/ImpactEffectsRuntimeAudit/*/summary.json)' \
		'    count=0' \
		'    if [ -n "${FAKE_IMPACT_AUDIT_CP_COUNT_FILE:-}" ] && [ -f "$FAKE_IMPACT_AUDIT_CP_COUNT_FILE" ]; then' \
		'      count=$(sed -n "1p" "$FAKE_IMPACT_AUDIT_CP_COUNT_FILE")' \
		'    fi' \
		'    count=$((count + 1))' \
		'    if [ -n "${FAKE_IMPACT_AUDIT_CP_COUNT_FILE:-}" ]; then' \
		'      printf "%s\\n" "$count" > "$FAKE_IMPACT_AUDIT_CP_COUNT_FILE"' \
		'    fi' \
		'    if [ "$count" -eq "${FAKE_IMPACT_AUDIT_CP_MUTATE_ON:-0}" ] && [ -n "${FAKE_IMPACT_AUDIT_CP_MUTATE_FILE:-}" ]; then' \
		'      printf "changed-during-report-copy\\n" >> "$FAKE_IMPACT_AUDIT_CP_MUTATE_FILE"' \
		'    fi' \
		'    ;;' \
		'esac' \
		'exec /bin/cp "$@"' > "$fixture_bin/cp"
	chmod +x "$fixture_bin/cp"
	printf '%s\n' "$fixture_bin"
}

prepare_recording_mv()
{
	test_root=$1
	fixture_bin=$test_root/mv-bin
	mkdir -p "$fixture_bin"
	printf '%s\n' \
		'#!/bin/sh' \
		'set -eu' \
		'if [ -n "${FAKE_IMPACT_AUDIT_MV_LOG_FILE:-}" ]; then' \
		'  printf "%s\\n" "$*" >> "$FAKE_IMPACT_AUDIT_MV_LOG_FILE"' \
		'fi' \
		'exec /bin/mv "$@"' > "$fixture_bin/mv"
	chmod +x "$fixture_bin/mv"
	printf '%s\n' "$fixture_bin"
}

test_sha256_file()
{
	if command -v shasum >/dev/null 2>&1; then
		shasum -a 256 "$1" | awk '{ print $1 }'
	else
		sha256sum "$1" | awk '{ print $1 }'
	fi
}

test_runtime_source_contract()
{
	[ -f "$audit_source" ] || fail "impact runtime audit source is missing"
	grep -F -q 'world.Map.Rules.Weapons' "$audit_source" || fail "resolved WeaponInfo is not used"
	grep -F -q '.Impact(Target.FromPos' "$audit_source" || fail "real positional WeaponInfo.Impact is missing"
	grep -F -q 'Target.FromActor' "$audit_source" || fail "real actor WeaponInfo.Impact is missing"
	grep -F -q 'world.Effects.OfType<SpriteEffect>()' "$audit_source" || fail "SpriteEffect observation is missing"
	grep -F -q 'Game.Renderer.SaveScreenshot' "$audit_source" || fail "case screenshot capture is missing"
	grep -F -q 'BLOCKED_RETAIL_ASSETS' "$audit_source" || fail "retail-resource block status is missing"
	grep -F -q 'BurstImpactCount' "$audit_source" || fail "continuous Kirov burst evidence is missing"
	grep -F -q 'OPENRA_IMPACT_EFFECT_AUDIT' "$effect_source" || fail "effect diagnostics are not environment-gated"
	grep -F -q 'ImpactEffectAudit.SpriteScheduled|' "$effect_source" || fail "scheduled sprite diagnostics are missing"
	grep -F -q 'ImpactEffectAudit.SpriteAdded|' "$effect_source" || fail "actual-added sprite diagnostics are missing"
	grep -F -q 'ImpactEffectAudit.Sound|' "$effect_source" || fail "sound playback diagnostics are missing"
	grep -F -q 'Game.Sound.DummyEngine' "$effect_source" || fail "Dummy audio evidence is missing"
	grep -F -q 'ImpactEffectsRuntimeAuditConfiguration.Parse' "$benchmark_source" || fail "impact audit config is not dispatched"
	grep -F -q 'ImpactEffectsRuntimeAuditSession.TryCreate' "$benchmark_source" || fail "impact audit session is not created"
	grep -F -q 'impactEffectsRuntimeAudit.Tick()' "$benchmark_source" || fail "impact audit session is not ticked"

	printf 'PASS: runtime source exposes the real World evidence boundaries\n'
}

test_copy_failure_still_stops_game()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-copy-failure.XXXXXX")
	set +e
	run_runner_at "$test_root" copy-failure "$failing_bin:" "$runner" >/dev/null 2>&1
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "the invalid audit unexpectedly succeeded"
	wait_for_file "$test_root/game.pid" 2 || fail "fake game pid was not recorded"
	game_pid=$(cat "$test_root/game.pid")
	if kill -0 "$game_pid" 2>/dev/null; then
		kill_test_process "$game_pid" impact-audit-fake-dotnet.sh
		fail "copy_report failure skipped game cleanup"
	fi

	printf 'PASS: copy failure still stops the owned game process\n'
}

test_term_escalates_and_reap_is_bounded()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-term-bound.XXXXXX")
	set +e
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=ignore \
	run_runner_at "$test_root" term-bound "" "$runner" >/dev/null 2>&1 &
	runner_pid=$!
	set -e
	wait_for_file "$test_root/game.pid" 3 || {
		kill_test_process "$runner_pid" run_impact_effects_runtime_audit.sh
		fail "fake game pid was not recorded"
	}
	game_pid=$(cat "$test_root/game.pid")
	if ! wait_for_exit "$runner_pid" 6; then
		kill_test_process "$runner_pid" run_impact_effects_runtime_audit.sh
		kill_test_process "$game_pid" impact-audit-fake-dotnet.sh
		fail "TERM-ignoring game made runner cleanup exceed its bound"
	fi

	if kill -0 "$game_pid" 2>/dev/null; then
		kill_test_process "$game_pid" impact-audit-fake-dotnet.sh
		fail "TERM-ignoring owned game survived cleanup escalation"
	fi

	printf 'PASS: TERM grace, KILL escalation, and reap are bounded\n'
}

test_launch_captures_full_process_identity()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-pid-capture.XXXXXX")
	prepare_fake_repo "$test_root"
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	FAKE_IMPACT_AUDIT_PS_CAPTURE_LOG=$test_root/ps-capture.log \
	run_runner_at "$test_root" pid-capture "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1 || {
		sed -n '1,240p' "$test_root/runner.log" >&2
		fail "passing fake audit failed while capturing process identity"
	}
	grep -F -q -- '-o ppid= -o lstart= -o command=' "$test_root/ps-capture.log" ||
		fail "runner did not capture the launch parent, start time, and full command together"

	printf 'PASS: launch captures start time plus the complete command identity\n'
}

test_launch_identity_retries_through_exec_transition()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-pid-retry.XXXXXX")
	prepare_fake_repo "$test_root"
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	FAKE_IMPACT_AUDIT_PS_TRANSIENT_COUNT=2 \
	FAKE_IMPACT_AUDIT_PS_IDENTITY_CALL_COUNT_FILE=$test_root/identity-count \
	run_runner_at "$test_root" pid-retry "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1 || {
		[ ! -f "$test_root/game.pid" ] || kill_test_process "$(cat "$test_root/game.pid")" impact-audit-fake-dotnet.sh
		sed -n '1,240p' "$test_root/runner.log" >&2
		fail "runner did not retry a transient pre-exec process identity"
	}
	identity_calls=$(cat "$test_root/identity-count")
	[ "$identity_calls" -ge 3 ] || fail "runner did not wait for the stable full command identity"
	game_pid=$(cat "$test_root/game.pid")
	if kill -0 "$game_pid" 2>/dev/null; then
		kill_test_process "$game_pid" impact-audit-fake-dotnet.sh
		fail "successful identity retry left the game child running"
	fi

	printf 'PASS: launch identity capture retries through the fork-to-exec transition\n'
}

test_never_matching_launch_identity_is_bounded_and_reaped()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-pid-never-match.XXXXXX")
	prepare_fake_repo "$test_root"
	started_at=$(date +%s)
	set +e
	FAKE_IMPACT_AUDIT_RESULT=hang \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	FAKE_IMPACT_AUDIT_PS_TRANSIENT_COUNT=100 \
	FAKE_IMPACT_AUDIT_PS_IDENTITY_CALL_COUNT_FILE=$test_root/identity-count \
	run_runner_at "$test_root" pid-never-match "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	elapsed=$(($(date +%s) - started_at))
	[ "$status" -ne 0 ] || fail "a launch identity that never matched unexpectedly succeeded"
	[ "$elapsed" -le 8 ] || fail "a launch identity that never matched exceeded the bounded capture window"
	game_pid=$(cat "$test_root/game.pid")
	if kill -0 "$game_pid" 2>/dev/null; then
		kill_test_process "$game_pid" impact-audit-fake-dotnet.sh
		fail "the still-owned direct launch child survived bounded identity failure"
	fi
	grep -F -q 'Unable to capture the expected dotnet/OpenRA/LaunchInto identity' "$test_root/runner.log" ||
		fail "bounded identity failure was not explained"

	printf 'PASS: a never-matching launch identity fails boundedly and reaps its direct child\n'
}

test_launch_identity_change_refuses_fallback_signal()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-pid-launch-change.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=hang \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	FAKE_IMPACT_AUDIT_PS_TRANSIENT_COUNT=100 \
	FAKE_IMPACT_AUDIT_PS_CHANGED_START_AFTER=1 \
	FAKE_IMPACT_AUDIT_PS_IDENTITY_CALL_COUNT_FILE=$test_root/identity-count \
	run_runner_at "$test_root" pid-launch-change "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "changed launch identity unexpectedly succeeded"
	game_pid=$(cat "$test_root/game.pid")
	if ! kill -0 "$game_pid" 2>/dev/null; then
		fail "runner signaled a PID whose launch start identity had changed"
	fi
	grep -F -q 'launch identity no longer matches' "$test_root/runner.log" || {
		kill -KILL "$game_pid" 2>/dev/null || true
		fail "launch-window signal refusal did not explain the identity mismatch"
	}
	kill -KILL "$game_pid" 2>/dev/null || true

	printf 'PASS: changed launch identity prevents the temporary ownership fallback from signaling\n'
}

test_launch_identity_retry_allows_early_exit_fallback()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-pid-early-exit.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=missing-exit \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	FAKE_IMPACT_AUDIT_PS_TRANSIENT_COUNT=100 \
	run_runner_at "$test_root" pid-early-exit "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 3 ] || {
		[ ! -f "$test_root/game.pid" ] || kill_test_process "$(cat "$test_root/game.pid")" impact-audit-fake-dotnet.sh
		sed -n '1,240p' "$test_root/runner.log" >&2
		fail "child exit during identity retry did not reach the retail-missing fallback"
	}
	grep -F -q '"status": "BLOCKED_RETAIL_ASSETS"' "$test_root/output/summary.json" ||
		fail "early launch exit did not publish the strict blocked summary"
	game_pid=$(cat "$test_root/game.pid")
	if kill -0 "$game_pid" 2>/dev/null; then
		kill_test_process "$game_pid" impact-audit-fake-dotnet.sh
		fail "early-exiting launch child was not reaped"
	fi

	printf 'PASS: child exit during identity retry follows the normal retail fallback\n'
}

test_start_identity_change_refuses_term()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-pid-start-change.XXXXXX")
	prepare_fake_repo "$test_root"
	ps_state=$test_root/ps-state
	set +e
	FAKE_IMPACT_AUDIT_RESULT=hang \
	FAKE_IMPACT_AUDIT_PS_STATE_FILE=$ps_state \
	FAKE_IMPACT_AUDIT_PS_CAPTURE_LOG=$test_root/ps-capture.log \
	FAKE_IMPACT_AUDIT_RUNNER_PID_FILE=$test_root/runner.pid \
	run_runner_at "$test_root" pid-start-change "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1 &
	runner_pid=$!
	set -e
	wait_for_file "$test_root/ps-capture.log" 3 || {
		kill_test_process "$runner_pid" run_impact_effects_runtime_audit.sh
		[ ! -f "$test_root/game.pid" ] || kill_test_process "$(cat "$test_root/game.pid")" impact-audit-fake-dotnet.sh
		fail "runner never captured the initial process identity"
	}
	wait_for_file "$test_root/runner.pid" 2 || {
		kill_test_process "$runner_pid" run_impact_effects_runtime_audit.sh
		[ ! -f "$test_root/game.pid" ] || kill_test_process "$(cat "$test_root/game.pid")" impact-audit-fake-dotnet.sh
		fail "fake game did not record its owning runner PID"
	}
	game_pid=$(cat "$test_root/game.pid")
	owned_runner_pid=$(cat "$test_root/runner.pid")
	printf 'changed-start\n' > "$ps_state"
	kill -TERM "$owned_runner_pid"
	set +e
	wait "$runner_pid"
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "identity mismatch was accepted as a successful cleanup"
	if ! kill -0 "$game_pid" 2>/dev/null; then
		fail "runner signaled a PID whose start identity had changed"
	fi
	grep -F -q 'identity no longer matches' "$test_root/runner.log" || {
		kill_test_process "$game_pid" impact-audit-fake-dotnet.sh
		fail "TERM refusal did not explain the identity mismatch"
	}
	kill_test_process "$game_pid" impact-audit-fake-dotnet.sh

	printf 'PASS: changed start identity prevents TERM\n'
}

test_identity_change_after_term_refuses_kill()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-pid-kill-change.XXXXXX")
	prepare_fake_repo "$test_root"
	ps_state=$test_root/ps-state
	set +e
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=ignore \
	FAKE_IMPACT_AUDIT_IDENTITY_CHANGE_ON_TERM=changed-start \
	FAKE_IMPACT_AUDIT_PS_STATE_FILE=$ps_state \
	run_runner_at "$test_root" pid-kill-change "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "identity mismatch before KILL was accepted"
	game_pid=$(cat "$test_root/game.pid")
	if ! kill -0 "$game_pid" 2>/dev/null; then
		fail "runner sent KILL after the owned PID identity changed"
	fi
	grep -F -q 'identity no longer matches' "$test_root/runner.log" || {
		kill_test_process "$game_pid" impact-audit-fake-dotnet.sh
		fail "KILL refusal did not explain the identity mismatch"
	}
	kill_test_process "$game_pid" impact-audit-fake-dotnet.sh

	printf 'PASS: identity change after TERM prevents KILL escalation\n'
}

test_term_exit_is_reaped()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-term-exit.XXXXXX")
	prepare_fake_repo "$test_root"
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	run_runner_at "$test_root" term-exit "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1 || {
		sed -n '1,240p' "$test_root/runner.log" >&2
		fail "TERM-exiting audit process was not reaped successfully"
	}
	game_pid=$(cat "$test_root/game.pid")
	if kill -0 "$game_pid" 2>/dev/null; then
		kill_test_process "$game_pid" impact-audit-fake-dotnet.sh
		fail "TERM-exiting child remained unreaped"
	fi

	printf 'PASS: a normally TERM-exiting child is reaped\n'
}

test_scoped_build_protocol_and_evidence_are_reported()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-build-evidence.XXXXXX")
	prepare_fake_repo "$test_root"
	if ! FAKE_IMPACT_AUDIT_BUILD_LOG=$test_root/build.log \
		FAKE_IMPACT_AUDIT_RESULT=passed \
		run_runner_at "$test_root" build-evidence "$controlled_bin:" \
			"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1; then
		sed -n '1,240p' "$test_root/runner.log" >&2
		fail "passing fake audit runner failed"
	fi
	grep -F -q 'build ' "$test_root/build.log" || fail "runner did not invoke a scoped build"
	grep -F -q 'OpenRA.Mods.RA2.csproj -c Release --no-restore' "$test_root/build.log" ||
		fail "runner build was not scoped Release/no-restore"
	[ -f "$test_root/output/evidence.sha256" ] || fail "evidence.sha256 was not reported"
	[ -f "$test_root/output/evidence.json" ] || fail "evidence.json was not reported"
	[ -f "$test_root/output/prebuild-inputs.sha256" ] || fail "pre-build input manifest was not preserved"
	[ -f "$test_root/output/build-evidence.sha256" ] || fail "bound build evidence was not preserved"
	[ "$(find "$test_root/output/screenshots" -type f -name '*.png' | wc -l | tr -d ' ')" -eq 16 ] ||
		fail "runner did not preserve exactly 16 screenshots"
	[ "$(find "$test_root/output/baselines" -type f -name '*.png' | wc -l | tr -d ' ')" -eq 16 ] ||
		fail "runner did not preserve exactly 16 pre-impact baselines"
	grep -F -q 'OpenRA.Mods.RA2/Traits/ImpactEffectsRuntimeAudit.cs' "$test_root/output/evidence.sha256" ||
		fail "runtime audit source hash is missing"
	grep -F -q 'packaging/run_impact_effects_runtime_audit.sh' "$test_root/output/evidence.sha256" ||
		fail "runner source hash is missing"
	grep -F -q 'engine/OpenRA.Mods.Common/Warheads/CreateEffectWarhead.cs' "$test_root/output/evidence.sha256" ||
		fail "effect diagnostic source hash is missing"
	grep -F -q 'engine/bin/OpenRA.Mods.RA2.dll' "$test_root/output/evidence.sha256" ||
		fail "built RA2 assembly hash is missing"
	grep -F -q 'engine/bin/OpenRA.Mods.Common.dll' "$test_root/output/evidence.sha256" ||
		fail "built Common assembly hash is missing"
	grep -F -q 'engine/bin/OpenRA.dll' "$test_root/output/evidence.sha256" ||
		fail "OpenRA entry assembly hash is missing"
	grep -F -q 'engine/bin/OpenRA.Game.dll' "$test_root/output/evidence.sha256" ||
		fail "OpenRA.Game dependency hash is missing"
	grep -F -q 'engine/bin/OpenRA.Platforms.Default.dll' "$test_root/output/evidence.sha256" ||
		fail "default platform dependency hash is missing"
	grep -F -q 'screenshots/impact-16.png' "$test_root/output/evidence.sha256" ||
		fail "screenshot hashes are missing"
	grep -F -q 'baselines/impact-16.png' "$test_root/output/evidence.sha256" ||
		fail "baseline screenshot hashes are missing"
	grep -F -q '8db6419f0b94b5341d3c21fe610595f923c2af76' "$test_root/output/evidence.json" ||
		fail "Git HEAD is missing from evidence metadata"

	printf 'PASS: runner performs isolated scoped build and hashes complete runtime evidence\n'
}

test_prebuild_manifest_rejects_build_time_change()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-prebuild-change.XXXXXX")
	prepare_fake_repo "$test_root"
	changed_file=$test_root/repo/OpenRA.Mods.RA2/Traits/ImpactEffectsRuntimeAudit.cs
	set +e
	FAKE_IMPACT_AUDIT_BUILD_MUTATE_FILE=$changed_file \
	FAKE_IMPACT_AUDIT_RESULT=passed \
	run_runner_at "$test_root" prebuild-change "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "build-time input change was accepted"
	grep -F -q 'Evidence hash changed after build' "$test_root/runner.log" ||
		fail "build-time mismatch did not identify the frozen input"

	printf 'PASS: pre-build manifest rejects an input changed during build\n'
}

test_final_evidence_reuses_frozen_build_hashes()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-final-frozen.XXXXXX")
	prepare_fake_repo "$test_root"
	cp_bin=$(prepare_counting_cp "$test_root")
	changed_file=$test_root/repo/engine/OpenRA.Mods.Common/Warheads/CreateEffectWarhead.cs
	set +e
	FAKE_IMPACT_AUDIT_CP_COUNT_FILE=$test_root/cp-count \
	FAKE_IMPACT_AUDIT_CP_MUTATE_FILE=$changed_file \
	FAKE_IMPACT_AUDIT_CP_MUTATE_ON=1 \
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	run_runner_at "$test_root" final-frozen "$cp_bin:$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "final evidence re-hashed and accepted a post-run source change"
	grep -F -q 'Final evidence hash mismatch' "$test_root/runner.log" ||
		fail "final evidence did not retain the frozen build hash"

	printf 'PASS: final evidence reuses frozen build hashes instead of re-hashing sources\n'
}

test_success_report_is_copied_once()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-copy-once.XXXXXX")
	prepare_fake_repo "$test_root"
	cp_bin=$(prepare_counting_cp "$test_root")
	FAKE_IMPACT_AUDIT_CP_COUNT_FILE=$test_root/cp-count \
	FAKE_IMPACT_AUDIT_CP_MUTATE_FILE=$test_root/support/Logs/ImpactEffectsRuntimeAudit/copy-once/summary.json \
	FAKE_IMPACT_AUDIT_CP_MUTATE_ON=2 \
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	run_runner_at "$test_root" copy-once "$cp_bin:$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1 || {
		sed -n '1,240p' "$test_root/runner.log" >&2
		fail "passing fake audit failed while checking single publication"
	}
	[ "$(sed -n '1p' "$test_root/cp-count")" -eq 1 ] ||
		fail "successful EXIT cleanup copied summary.json more than once"
	recorded_hash=$(awk '$2 == "summary.json" { print $1 }' "$test_root/output/evidence.sha256")
	actual_hash=$(test_sha256_file "$test_root/output/summary.json")
	[ "$recorded_hash" = "$actual_hash" ] ||
		fail "published summary changed after final evidence verification"

	printf 'PASS: successful report publication is copied exactly once\n'
}

test_success_report_is_staged_and_summary_is_published_last()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-staged-publish.XXXXXX")
	prepare_fake_repo "$test_root"
	cp_bin=$(prepare_counting_cp "$test_root")
	mv_bin=$(prepare_recording_mv "$test_root")
	FAKE_IMPACT_AUDIT_CP_LOG_FILE=$test_root/cp.log \
	FAKE_IMPACT_AUDIT_MV_LOG_FILE=$test_root/mv.log \
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	run_runner_at "$test_root" staged-publish "$cp_bin:$mv_bin:$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1 || {
		sed -n '1,240p' "$test_root/runner.log" >&2
		fail "passing fake audit failed while checking staged publication"
	}
	grep -F -q '/.report-staging/summary.json' "$test_root/cp.log" ||
		fail "summary report was copied directly into the published output"
	last_report_move=$(awk '/\.report-staging\// { line = NR } END { print line + 0 }' "$test_root/mv.log")
	summary_move=$(awk '/\.report-staging\/summary\.json / { line = NR } END { print line + 0 }' "$test_root/mv.log")
	[ "$summary_move" -gt 0 ] || fail "staged summary was never published"
	[ "$summary_move" -eq "$last_report_move" ] || fail "summary was not the last staged report published"
	[ ! -d "$test_root/output/.report-staging" ] || fail "successful report staging directory was not emptied"

	printf 'PASS: validated report is staged and publishes summary last\n'
}

test_missing_baseline_prevents_success_publication()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-missing-baseline.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_BASELINE_COUNT=15 \
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	run_runner_at "$test_root" missing-baseline "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 1 ] || fail "success with only 15 baselines was accepted"
	[ ! -f "$test_root/output/summary.json" ] || fail "invalid success summary was published before baseline validation"
	[ -f "$test_root/output/.report-staging/summary.json" ] ||
		fail "failed-path report was not retained in staging for diagnosis"
	grep -F -q 'expected 16 baseline screenshots' "$test_root/runner.log" ||
		fail "baseline gate failure was not explicit"

	printf 'PASS: 16 baseline files are required before success publication\n'
}

test_evidence_files_use_atomic_temp_renames()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-evidence-rename.XXXXXX")
	prepare_fake_repo "$test_root"
	mv_bin=$(prepare_recording_mv "$test_root")
	FAKE_IMPACT_AUDIT_MV_LOG_FILE=$test_root/mv.log \
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	run_runner_at "$test_root" evidence-rename "$mv_bin:$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1 || {
		sed -n '1,240p' "$test_root/runner.log" >&2
		fail "passing fake audit failed while checking evidence rename"
	}
	grep -E -q 'evidence\.sha256\.tmp .*evidence\.sha256$' "$test_root/mv.log" ||
		fail "evidence.sha256 was not published by temp-file rename"
	grep -E -q 'evidence\.json\.tmp .*evidence\.json$' "$test_root/mv.log" ||
		fail "evidence.json was not published by temp-file rename"
	if find "$test_root/output" -type f -name '*.tmp' | grep -q .; then
		fail "successful evidence publication left temp files behind"
	fi

	printf 'PASS: evidence manifest and metadata use atomic temp-file renames\n'
}

test_missing_asset_launch_fallback_is_blocked()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-missing-fallback.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=missing-exit \
	run_runner_at "$test_root" missing-fallback "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 3 ] || fail "missing-asset fallback did not exit with status 3"
	grep -F -q 'BLOCKED_RETAIL_ASSETS' "$test_root/output/summary.json" ||
		fail "missing-asset fallback did not publish its explicit block status"

	printf 'PASS: missing semantics plus a known retail asset produce BLOCKED\n'
}

test_decode_error_launch_fallback_is_failed()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-decode-fallback.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=decode-exit \
	run_runner_at "$test_root" decode-fallback "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 1 ] || fail "decode error mentioning an asset was not FAILED"
	if [ -f "$test_root/output/summary.json" ] &&
		grep -F -q 'BLOCKED_RETAIL_ASSETS' "$test_root/output/summary.json"; then
		fail "decode error was downgraded to a retail-assets block"
	fi

	printf 'PASS: decode/error text with an asset name remains FAILED\n'
}

test_decoder_lookup_launch_fallback_is_failed()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-decoder-fallback.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=decoder-find-exit \
	run_runner_at "$test_root" decoder-fallback "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 1 ] || fail "decoder lookup mentioning an asset was not FAILED"
	if [ -f "$test_root/output/summary.json" ] &&
		grep -F -q 'BLOCKED_RETAIL_ASSETS' "$test_root/output/summary.json"; then
		fail "decoder lookup was downgraded to a retail-assets block"
	fi

	printf 'PASS: decoder lookup text with an asset name remains FAILED\n'
}

test_missing_warning_plus_fatal_is_failed()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-missing-fatal.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=missing-warning-fatal-exit \
	run_runner_at "$test_root" missing-fatal "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 1 ] || fail "fatal launch after a missing warning was not FAILED"
	if [ -f "$test_root/output/summary.json" ] &&
		grep -F -q 'BLOCKED_RETAIL_ASSETS' "$test_root/output/summary.json"; then
		fail "fatal launch was downgraded because of an earlier missing warning"
	fi

	printf 'PASS: a fatal launch cannot be downgraded by a missing warning\n'
}

test_file_not_found_exception_is_blocked()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-file-not-found.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=file-not-found-exception-exit \
	run_runner_at "$test_root" file-not-found "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 3 ] || fail "FileNotFoundException for a known retail asset was not BLOCKED"
	grep -F -q 'BLOCKED_RETAIL_ASSETS' "$test_root/output/summary.json" ||
		fail "FileNotFoundException block summary was not published"

	printf 'PASS: FileNotFoundException for a known retail asset is BLOCKED\n'
}

test_legacy_pass_without_final_protocol_is_rejected()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-final-protocol.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	FAKE_IMPACT_AUDIT_OMIT_FINAL_PROTOCOL=true \
	run_runner_at "$test_root" final-protocol "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 1 ] || fail "legacy PASS without final functional/performance/burst evidence was accepted"
	grep -F -q 'required 16/16 plus burst evidence' "$test_root/runner.log" ||
		fail "final protocol rejection was not explicit"

	printf 'PASS: final protocol requires functional/performance and actual burst lifecycle evidence\n'
}

assert_invalid_summary_variant_rejected()
{
	test_root=$1
	variant=$2
	run_id=$3
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=passed \
	FAKE_IMPACT_AUDIT_TERM_BEHAVIOR=exit \
	FAKE_IMPACT_AUDIT_SUMMARY_VARIANT=$variant \
	run_runner_at "$test_root" "$run_id" "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 1 ] || fail "$variant summary was not rejected with exit status 1"
	grep -F -q 'Invalid impact runtime audit summary JSON' "$test_root/runner.log" ||
		fail "$variant summary did not produce an explicit strict-JSON error"
	[ ! -f "$test_root/output/summary.json" ] || fail "$variant summary was published"
}

test_malformed_summary_json_is_rejected()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-json-malformed.XXXXXX")
	assert_invalid_summary_variant_rejected "$test_root" malformed json-malformed
	printf 'PASS: malformed summary JSON is rejected\n'
}

test_duplicate_summary_key_is_rejected()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-json-duplicate.XXXXXX")
	assert_invalid_summary_variant_rejected "$test_root" duplicate json-duplicate
	printf 'PASS: duplicate summary JSON keys are rejected\n'
}

test_wrong_summary_field_type_is_rejected()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-json-type.XXXXXX")
	assert_invalid_summary_variant_rejected "$test_root" wrong-type json-type
	printf 'PASS: bool cannot satisfy an integer summary field\n'
}

test_zero_performance_samples_are_rejected()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-json-zero-samples.XXXXXX")
	assert_invalid_summary_variant_rejected "$test_root" zero-samples json-zero-samples
	printf 'PASS: successful summary requires a positive sample_count\n'
}

test_missing_performance_metrics_are_rejected()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-json-metrics.XXXXXX")
	assert_invalid_summary_variant_rejected "$test_root" missing-metrics json-metrics
	printf 'PASS: missing p95/max metric objects are rejected\n'
}

test_nonfinite_performance_metric_is_rejected()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-json-nonfinite.XXXXXX")
	assert_invalid_summary_variant_rejected "$test_root" nonfinite json-nonfinite
	printf 'PASS: NaN performance metrics are rejected\n'
}

test_invalid_python_releases_lock()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-invalid-python.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	IMPACT_TEST_PYTHON_OVERRIDE=$test_root/not-executable-python \
	FAKE_IMPACT_AUDIT_RESULT=passed \
	run_runner_at "$test_root" invalid-python "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 2 ] || fail "invalid Python dependency did not exit with status 2"
	assert_lock_absent "$test_root/repo/.openra-runtime-audit.lock" \
		"invalid Python dependency left the repository audit lock behind"
	grep -F -q 'Python 3' "$test_root/runner.log" || fail "invalid Python dependency was not explicit"

	printf 'PASS: invalid strict-JSON dependency releases the acquired lock\n'
}

test_blocked_retail_assets_is_not_a_false_pass()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-retail-block.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=blocked \
	run_runner_at "$test_root" retail-block "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 3 ] || fail "retail block did not use the explicit exit status 3"
	grep -F -q 'BLOCKED_RETAIL_ASSETS' "$test_root/output/summary.json" ||
		fail "retail block summary was not preserved"
	if grep -F -q '"status": "PASSED"' "$test_root/output/summary.json"; then
		fail "retail resource block was reported as a pass"
	fi

	printf 'PASS: missing retail assets are explicit and never reported as green\n'
}

test_atomic_lock_refuses_concurrent_runner()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-lock.XXXXXX")
	prepare_fake_repo "$test_root"
	mkdir "$test_root/repo/.openra-runtime-audit.lock"
	printf 'other-run\n' > "$test_root/repo/.openra-runtime-audit.lock/run-id"
	set +e
	FAKE_IMPACT_AUDIT_RESULT=passed \
	run_runner_at "$test_root" lock-refusal "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "concurrent audit lock was ignored"
	grep -F -q 'runtime audit lock' "$test_root/runner.log" || fail "lock refusal was not explicit"
	[ -f "$test_root/repo/.openra-runtime-audit.lock/run-id" ] || fail "runner removed another audit's lock"

	printf 'PASS: repository atomic lock rejects concurrent audit runners\n'
}

test_invalid_dotnet_releases_lock()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-invalid-dotnet-lock.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	IMPACT_TEST_DOTNET_OVERRIDE=$test_root/not-executable-dotnet \
	run_runner_at "$test_root" invalid-dotnet "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "invalid dotnet unexpectedly succeeded"
	assert_lock_absent "$test_root/repo/.openra-runtime-audit.lock" \
		"invalid dotnet left the repository audit lock behind"

	printf 'PASS: invalid dotnet releases the acquired audit lock\n'
}

test_build_failure_releases_lock()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-build-failure-lock.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_BUILD_EXIT_CODE=19 \
	run_runner_at "$test_root" build-failure "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 19 ] || fail "build failure status was not preserved"
	assert_lock_absent "$test_root/repo/.openra-runtime-audit.lock" \
		"build failure left the repository audit lock behind"

	printf 'PASS: scoped build failure releases the acquired audit lock\n'
}

test_signal_after_lock_releases_lock()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-signal-lock.XXXXXX")
	prepare_fake_repo "$test_root"
	set +e
	FAKE_IMPACT_AUDIT_SIGNAL_PARENT=TERM \
	run_runner_at "$test_root" signal-lock "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 130 ] || fail "signal did not produce the runner's explicit status 130"
	assert_lock_absent "$test_root/repo/.openra-runtime-audit.lock" \
		"signal after lock acquisition left the repository audit lock behind"

	printf 'PASS: signal after acquisition releases the audit lock\n'
}

test_success_releases_lock()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-success-lock.XXXXXX")
	prepare_fake_repo "$test_root"
	FAKE_IMPACT_AUDIT_RESULT=passed \
	run_runner_at "$test_root" success-lock "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1 || {
		sed -n '1,240p' "$test_root/runner.log" >&2
		fail "passing fake audit failed while checking lock release"
	}
	assert_lock_absent "$test_root/repo/.openra-runtime-audit.lock" \
		"successful audit left the repository audit lock behind"

	printf 'PASS: successful audit releases the acquired lock\n'
}

test_replaced_lock_is_not_removed()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-replaced-lock.XXXXXX")
	prepare_fake_repo "$test_root"
	lock_dir=$test_root/repo/.openra-runtime-audit.lock
	set +e
	FAKE_IMPACT_AUDIT_BUILD_EXIT_CODE=23 \
	FAKE_IMPACT_AUDIT_REPLACE_LOCK_DIR=$lock_dir \
	run_runner_at "$test_root" replaced-lock "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -eq 23 ] || fail "replacement-lock build failure status was not preserved"
	[ -d "$lock_dir" ] || fail "runner removed a replacement lock directory"
	grep -F -q 'identity changed' "$test_root/runner.log" ||
		fail "runner did not explain the lock identity mismatch"

	printf 'PASS: cleanup refuses to remove a replacement lock directory\n'
}

test_evidence_gate_rejects_post_build_change()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/impact-audit-evidence-change.XXXXXX")
	prepare_fake_repo "$test_root"
	changed_file=$test_root/repo/engine/OpenRA.Mods.Common/Warheads/CreateEffectWarhead.cs
	set +e
	FAKE_IMPACT_AUDIT_BUILD_LOG=$test_root/build.log \
	FAKE_IMPACT_AUDIT_MUTATE_FILE=$changed_file \
	FAKE_IMPACT_AUDIT_RESULT=passed \
	run_runner_at "$test_root" evidence-change "$controlled_bin:" \
		"$test_root/repo/packaging/run_impact_effects_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "post-build evidence change was accepted"
	grep -F -q 'Evidence hash changed after build' "$test_root/runner.log" ||
		fail "evidence mismatch did not identify the freshness failure"

	printf 'PASS: evidence gate rejects source or assembly changes after build\n'
}

case "$test_case" in
	source-contract) test_runtime_source_contract ;;
	copy-failure) test_copy_failure_still_stops_game ;;
	term-escalation) test_term_escalates_and_reap_is_bounded ;;
	pid-capture) test_launch_captures_full_process_identity ;;
	pid-retry) test_launch_identity_retries_through_exec_transition ;;
	pid-never-match) test_never_matching_launch_identity_is_bounded_and_reaped ;;
	pid-launch-change) test_launch_identity_change_refuses_fallback_signal ;;
	pid-early-exit) test_launch_identity_retry_allows_early_exit_fallback ;;
	pid-term-mismatch) test_start_identity_change_refuses_term ;;
	pid-kill-mismatch) test_identity_change_after_term_refuses_kill ;;
	pid-term-exit) test_term_exit_is_reaped ;;
	build-evidence) test_scoped_build_protocol_and_evidence_are_reported ;;
	prebuild-change) test_prebuild_manifest_rejects_build_time_change ;;
	final-frozen-evidence) test_final_evidence_reuses_frozen_build_hashes ;;
	copy-once) test_success_report_is_copied_once ;;
	staged-publish) test_success_report_is_staged_and_summary_is_published_last ;;
	baseline-gate) test_missing_baseline_prevents_success_publication ;;
	evidence-rename) test_evidence_files_use_atomic_temp_renames ;;
	missing-fallback) test_missing_asset_launch_fallback_is_blocked ;;
	decode-fallback) test_decode_error_launch_fallback_is_failed ;;
	decoder-fallback) test_decoder_lookup_launch_fallback_is_failed ;;
	missing-fatal-fallback) test_missing_warning_plus_fatal_is_failed ;;
	file-not-found-fallback) test_file_not_found_exception_is_blocked ;;
	final-protocol) test_legacy_pass_without_final_protocol_is_rejected ;;
	json-malformed) test_malformed_summary_json_is_rejected ;;
	json-duplicate) test_duplicate_summary_key_is_rejected ;;
	json-type) test_wrong_summary_field_type_is_rejected ;;
	json-zero-samples) test_zero_performance_samples_are_rejected ;;
	json-metrics) test_missing_performance_metrics_are_rejected ;;
	json-nonfinite) test_nonfinite_performance_metric_is_rejected ;;
	invalid-python-lock) test_invalid_python_releases_lock ;;
	retail-block) test_blocked_retail_assets_is_not_a_false_pass ;;
	atomic-lock) test_atomic_lock_refuses_concurrent_runner ;;
	invalid-dotnet-lock) test_invalid_dotnet_releases_lock ;;
	build-failure-lock) test_build_failure_releases_lock ;;
	signal-lock) test_signal_after_lock_releases_lock ;;
	success-lock) test_success_releases_lock ;;
	replaced-lock) test_replaced_lock_is_not_removed ;;
	evidence-change) test_evidence_gate_rejects_post_build_change ;;
	all)
		test_runtime_source_contract
		test_copy_failure_still_stops_game
		test_term_escalates_and_reap_is_bounded
		test_launch_captures_full_process_identity
		test_launch_identity_retries_through_exec_transition
		test_never_matching_launch_identity_is_bounded_and_reaped
		test_launch_identity_change_refuses_fallback_signal
		test_launch_identity_retry_allows_early_exit_fallback
		test_start_identity_change_refuses_term
		test_identity_change_after_term_refuses_kill
		test_term_exit_is_reaped
		test_scoped_build_protocol_and_evidence_are_reported
		test_prebuild_manifest_rejects_build_time_change
		test_final_evidence_reuses_frozen_build_hashes
		test_success_report_is_copied_once
		test_success_report_is_staged_and_summary_is_published_last
		test_missing_baseline_prevents_success_publication
		test_evidence_files_use_atomic_temp_renames
		test_missing_asset_launch_fallback_is_blocked
		test_decode_error_launch_fallback_is_failed
		test_decoder_lookup_launch_fallback_is_failed
		test_missing_warning_plus_fatal_is_failed
		test_file_not_found_exception_is_blocked
		test_legacy_pass_without_final_protocol_is_rejected
		test_malformed_summary_json_is_rejected
		test_duplicate_summary_key_is_rejected
		test_wrong_summary_field_type_is_rejected
		test_zero_performance_samples_are_rejected
		test_missing_performance_metrics_are_rejected
		test_nonfinite_performance_metric_is_rejected
		test_invalid_python_releases_lock
		test_blocked_retail_assets_is_not_a_false_pass
		test_atomic_lock_refuses_concurrent_runner
		test_invalid_dotnet_releases_lock
		test_build_failure_releases_lock
		test_signal_after_lock_releases_lock
		test_success_releases_lock
		test_replaced_lock_is_not_removed
		test_evidence_gate_rejects_post_build_change
		;;
	*) fail "unknown test case: $test_case" ;;
esac
