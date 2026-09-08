#!/bin/sh
# Applies the local engine patch series (engine-patches/*.patch, in filename
# order) to a pristine engine checkout. fetch-engine.sh runs this automatically
# after downloading a fresh engine. Manual use:
#
#     rm -rf engine && ./fetch-engine.sh        # fetch + patch, or
#     sh engine-patches/apply-engine-patches.sh # re-apply onto a pristine engine
#
# Patches are NOT idempotent: applying twice on the same tree will fail.

set -e

TEMPLATE_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "${TEMPLATE_ROOT}"

# shellcheck source=mod.config
. "${TEMPLATE_ROOT}/mod.config"

if [ -f "${TEMPLATE_ROOT}/user.config" ]; then
	# shellcheck source=user.config
	. "${TEMPLATE_ROOT}/user.config"
fi

if [ ! -d "${ENGINE_DIRECTORY}" ]; then
	echo "Engine directory '${ENGINE_DIRECTORY}' does not exist; run ./fetch-engine.sh first."
	exit 1
fi

for patch_file in "${TEMPLATE_ROOT}"/engine-patches/*.patch; do
	[ -e "${patch_file}" ] || continue
	echo "Applying $(basename "${patch_file}")..."
	patch -p1 -s -d "${ENGINE_DIRECTORY}" < "${patch_file}"
done

echo "All engine patches applied."
