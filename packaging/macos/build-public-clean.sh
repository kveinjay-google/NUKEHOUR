#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PROJECT_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
OUTPUT_DIR="$PROJECT_ROOT/artifacts/public-macos"

mkdir -p "$OUTPUT_DIR"
exec python3 "$SCRIPT_DIR/build_public_clean.py" \
  --output "$OUTPUT_DIR" \
  "$@"
