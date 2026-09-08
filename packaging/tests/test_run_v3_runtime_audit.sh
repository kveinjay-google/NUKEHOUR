#!/bin/sh
set -eu

repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
runner=$repo_root/packaging/run_v3_runtime_audit.sh
fake_dotnet=$repo_root/packaging/tests/fixtures/v3-audit-fake-dotnet.sh
failing_bin=$repo_root/packaging/tests/fixtures/failing-bin
controlled_bin=$repo_root/packaging/tests/fixtures/controlled-bin
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

run_runner()
{
	test_root=$1
	run_id=$2
	path_prefix=$3
	run_runner_at "$test_root" "$run_id" "$path_prefix" "$runner"
}

run_runner_at()
{
	test_root=$1
	run_id=$2
	path_prefix=$3
	runner_path=$4
	mkdir -p "$test_root/support"
	PATH="$path_prefix$PATH" \
	OPENRA_V3_RUNTIME_AUDIT_DOTNET="$fake_dotnet" \
	OPENRA_V3_RUNTIME_AUDIT_SUPPORT_DIR="$test_root/support" \
	OPENRA_V3_RUNTIME_AUDIT_OUTPUT="$test_root/output" \
	OPENRA_V3_RUNTIME_AUDIT_RUN_ID="$run_id" \
	OPENRA_V3_RUNTIME_AUDIT_RUN_TIMEOUT_SECONDS=10 \
	OPENRA_V3_RUNTIME_AUDIT_TERM_GRACE_SECONDS=1 \
	OPENRA_V3_RUNTIME_AUDIT_REAP_GRACE_SECONDS=1 \
	FAKE_V3_AUDIT_PID_FILE="$test_root/game.pid" \
	FAKE_V3_AUDIT_BUILD_LOG="${FAKE_V3_AUDIT_BUILD_LOG:-}" \
	FAKE_V3_AUDIT_MUTATE_FILE="${FAKE_V3_AUDIT_MUTATE_FILE:-}" \
	FAKE_V3_AUDIT_PASS="${FAKE_V3_AUDIT_PASS:-false}" \
	"$runner_path"
}

prepare_fake_repo()
{
	test_root=$1
	fake_repo=$test_root/repo
	mkdir -p \
		"$fake_repo/packaging" \
		"$fake_repo/OpenRA.Mods.RA2/Traits" \
		"$fake_repo/OpenRA.Mods.RA2/Activities" \
		"$fake_repo/mods/ra2/rules" \
		"$fake_repo/docs/testing/fixtures/v3-legacy-8db6419" \
		"$fake_repo/engine/bin"
	cp "$runner" "$fake_repo/packaging/run_v3_runtime_audit.sh"
	printf '<Project />\n' > "$fake_repo/OpenRA.Mods.RA2/OpenRA.Mods.RA2.csproj"
	printf 'runtime audit source\n' > "$fake_repo/OpenRA.Mods.RA2/Traits/V3RuntimeAudit.cs"
	printf 'missile trait source\n' > "$fake_repo/OpenRA.Mods.RA2/Traits/BallisticMissile.cs"
	printf 'missile activity source\n' > "$fake_repo/OpenRA.Mods.RA2/Activities/BallisticMissileFly.cs"
	printf 'v3 rules\n' > "$fake_repo/mods/ra2/rules/soviet-vehicles.yaml"
	printf 'fixture\n' > "$fake_repo/docs/testing/fixtures/v3-legacy-8db6419/trajectory.csv"
	printf 'provenance\n' > "$fake_repo/docs/testing/fixtures/v3-legacy-8db6419/provenance.json"
	printf 'engine\n' > "$fake_repo/engine/bin/OpenRA.dll"
	printf 'ra2 assembly\n' > "$fake_repo/engine/bin/OpenRA.Mods.RA2.dll"
}

test_copy_failure_still_stops_game()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/v3-audit-copy-failure.XXXXXX")
	set +e
	run_runner "$test_root" copy-failure "$failing_bin:" >/dev/null 2>&1
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "the invalid audit unexpectedly succeeded"
	wait_for_file "$test_root/game.pid" 2 || fail "fake game pid was not recorded"
	game_pid=$(cat "$test_root/game.pid")
	if kill -0 "$game_pid" 2>/dev/null; then
		kill_test_process "$game_pid" v3-audit-fake-dotnet.sh
		fail "copy_report failure skipped game cleanup"
	fi

	printf 'PASS: copy failure still stops the owned game process\n'
}

test_term_escalates_and_reap_is_bounded()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/v3-audit-term-bound.XXXXXX")
	set +e
	run_runner "$test_root" term-bound "" >/dev/null 2>&1 &
	runner_pid=$!
	set -e
	wait_for_file "$test_root/game.pid" 3 || {
		kill_test_process "$runner_pid" run_v3_runtime_audit.sh
		fail "fake game pid was not recorded"
	}
	game_pid=$(cat "$test_root/game.pid")
	if ! wait_for_exit "$runner_pid" 6; then
		kill_test_process "$runner_pid" run_v3_runtime_audit.sh
		kill_test_process "$game_pid" v3-audit-fake-dotnet.sh
		fail "TERM-ignoring game made runner cleanup exceed its bound"
	fi

	if kill -0 "$game_pid" 2>/dev/null; then
		kill_test_process "$game_pid" v3-audit-fake-dotnet.sh
		fail "TERM-ignoring owned game survived cleanup escalation"
	fi

	printf 'PASS: TERM grace, KILL escalation, and reap are bounded\n'
}

test_scoped_build_and_evidence_are_reported()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/v3-audit-build-evidence.XXXXXX")
	prepare_fake_repo "$test_root"
	FAKE_V3_AUDIT_BUILD_LOG=$test_root/build.log \
	FAKE_V3_AUDIT_PASS=true \
	run_runner_at "$test_root" build-evidence "$controlled_bin:" \
		"$test_root/repo/packaging/run_v3_runtime_audit.sh" >/dev/null 2>&1
	grep -F -q 'build ' "$test_root/build.log" || fail "runner did not invoke a scoped build"
	grep -F -q 'OpenRA.Mods.RA2.csproj -c Release --no-restore' "$test_root/build.log" ||
		fail "runner build was not scoped Release/no-restore"
	[ -f "$test_root/output/evidence.sha256" ] || fail "evidence.sha256 was not reported"
	[ -f "$test_root/output/evidence.json" ] || fail "evidence.json was not reported"
	grep -F -q 'OpenRA.Mods.RA2/Traits/V3RuntimeAudit.cs' "$test_root/output/evidence.sha256" ||
		fail "runtime audit source hash is missing"
	grep -F -q 'OpenRA.Mods.RA2/Traits/BallisticMissile.cs' "$test_root/output/evidence.sha256" ||
		fail "missile trait source hash is missing"
	grep -F -q 'OpenRA.Mods.RA2/Activities/BallisticMissileFly.cs' "$test_root/output/evidence.sha256" ||
		fail "missile activity source hash is missing"
	grep -F -q 'mods/ra2/rules/soviet-vehicles.yaml' "$test_root/output/evidence.sha256" ||
		fail "V3 rules hash is missing"
	grep -F -q 'docs/testing/fixtures/v3-legacy-8db6419/trajectory.csv' "$test_root/output/evidence.sha256" ||
		fail "independent legacy trajectory fixture hash is missing"
	grep -F -q 'docs/testing/fixtures/v3-legacy-8db6419/provenance.json' "$test_root/output/evidence.sha256" ||
		fail "legacy fixture provenance hash is missing"
	grep -F -q 'engine/bin/OpenRA.Mods.RA2.dll' "$test_root/output/evidence.sha256" ||
		fail "built RA2 assembly hash is missing"
	grep -F -q '8db6419f0b94b5341d3c21fe610595f923c2af76' "$test_root/output/evidence.json" ||
		fail "Git HEAD is missing from evidence metadata"

	printf 'PASS: runner performs scoped Release build and reports evidence hashes\n'
}

test_evidence_gate_rejects_post_build_change()
{
	test_root=$(mktemp -d "${TMPDIR:-/tmp}/v3-audit-evidence-change.XXXXXX")
	prepare_fake_repo "$test_root"
	changed_file=$test_root/repo/OpenRA.Mods.RA2/Activities/BallisticMissileFly.cs
	set +e
	FAKE_V3_AUDIT_BUILD_LOG=$test_root/build.log \
	FAKE_V3_AUDIT_MUTATE_FILE=$changed_file \
	FAKE_V3_AUDIT_PASS=true \
	run_runner_at "$test_root" evidence-change "$controlled_bin:" \
		"$test_root/repo/packaging/run_v3_runtime_audit.sh" >"$test_root/runner.log" 2>&1
	status=$?
	set -e
	[ "$status" -ne 0 ] || fail "post-build evidence change was accepted"
	grep -F -q 'Evidence hash changed after build' "$test_root/runner.log" ||
		fail "evidence mismatch did not identify the freshness failure"

	printf 'PASS: evidence gate rejects source or assembly changes after build\n'
}

case "$test_case" in
	copy-failure) test_copy_failure_still_stops_game ;;
	term-escalation) test_term_escalates_and_reap_is_bounded ;;
	build-evidence) test_scoped_build_and_evidence_are_reported ;;
	evidence-change) test_evidence_gate_rejects_post_build_change ;;
	all)
		test_copy_failure_still_stops_game
		test_term_escalates_and_reap_is_bounded
		test_scoped_build_and_evidence_are_reported
		test_evidence_gate_rejects_post_build_change
		;;
	*) fail "unknown test case: $test_case" ;;
esac
