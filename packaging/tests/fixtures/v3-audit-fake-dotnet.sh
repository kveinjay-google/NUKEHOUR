#!/bin/sh
set -eu

if [ "${1:-}" = "build" ]; then
	if [ -n "${FAKE_V3_AUDIT_BUILD_LOG:-}" ]; then
		printf '%s\n' "$*" >> "$FAKE_V3_AUDIT_BUILD_LOG"
	fi

	exit "${FAKE_V3_AUDIT_BUILD_EXIT_CODE:-0}"
fi

: "${FAKE_V3_AUDIT_PID_FILE:?}"
: "${OPENRA_V3_RUNTIME_AUDIT_SUPPORT_DIR:?}"
: "${OPENRA_V3_RUNTIME_AUDIT_RUN_ID:?}"

printf '%s\n' "$$" > "$FAKE_V3_AUDIT_PID_FILE"
runtime_dir=$OPENRA_V3_RUNTIME_AUDIT_SUPPORT_DIR/Logs/V3RuntimeAudit/$OPENRA_V3_RUNTIME_AUDIT_RUN_ID
mkdir -p "$runtime_dir"
if [ -n "${FAKE_V3_AUDIT_MUTATE_FILE:-}" ]; then
	printf 'changed-after-evidence\n' >> "$FAKE_V3_AUDIT_MUTATE_FILE"
fi

if [ "${FAKE_V3_AUDIT_PASS:-false}" = "true" ]; then
	printf '{\n  "complete": true,\n  "total": 9,\n  "completed": 9,\n  "passed": 9,\n  "failed": 0\n}\n' > "$runtime_dir/summary.json"
else
	printf '{\n  "complete": true,\n  "total": 9,\n  "completed": 9,\n  "passed": 8,\n  "failed": 1\n}\n' > "$runtime_dir/summary.json"
fi

trap '' TERM
while :
do
	sleep 1
done
