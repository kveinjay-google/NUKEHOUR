#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPOSITORY_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
MANIFEST="$REPOSITORY_ROOT/packaging/public-content-manifest.json"
ARTIFACT_DIR="${PUBLIC_ARTIFACT_DIR:-$REPOSITORY_ROOT/artifacts/public-ios}"
BUILD_ROOT="${PUBLIC_BUILD_ROOT:-$REPOSITORY_ROOT/artifacts/build-public}"
APP="$BUILD_ROOT/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app"
IPA="$ARTIFACT_DIR/NUKE HOUR-PublicClean.ipa"

if [ ! -f "$MANIFEST" ]; then
	printf 'Missing public content manifest: %s\n' "$MANIFEST" >&2
	exit 1
fi

if [ -z "${IOS_CODESIGN_KEY:-}" ] || [ -z "${IOS_PROVISION_UUID:-}" ]; then
	printf 'Set IOS_CODESIGN_KEY and IOS_PROVISION_UUID for the signed public build.\n' >&2
	exit 1
fi

if [ -x "$HOME/.dotnet/dotnet" ]; then
	DOTNET="$HOME/.dotnet/dotnet"
	PATH="$HOME/.dotnet:$PATH"
	export PATH
	DOTNET_ROOT="$HOME/.dotnet"
	export DOTNET_ROOT
else
	DOTNET=dotnet
fi

mkdir -p "$ARTIFACT_DIR" "$BUILD_ROOT"

"$DOTNET" build "$REPOSITORY_ROOT/ios/OpenRA.iOS/OpenRA.iOS.csproj" \
	-c Release \
	-p:RuntimeIdentifier=ios-arm64 \
	-p:PublicClean=true \
	-p:BaseOutputPath="$BUILD_ROOT/" \
	-p:CodesignKey="$IOS_CODESIGN_KEY" \
	-p:CodesignProvision="$IOS_PROVISION_UUID" \
	--no-restore

python3 "$REPOSITORY_ROOT/packaging/public_bundle_audit.py" \
	--root "$APP" \
	--manifest "$MANIFEST" \
	--json-report "$ARTIFACT_DIR/public-bundle-audit.json" \
	--markdown-report "$ARTIFACT_DIR/public-bundle-audit.md"

PACKAGE_DIR=$(mktemp -d "${TMPDIR:-/tmp}/openra-public-package.XXXXXX")
trap 'rm -rf "$PACKAGE_DIR"' EXIT HUP INT TERM
mkdir -p "$PACKAGE_DIR/Payload"
ditto "$APP" "$PACKAGE_DIR/Payload/OpenRA.iOS.app"
(cd "$PACKAGE_DIR" && ditto -c -k --sequesterRsrc --keepParent Payload "$IPA")

python3 "$REPOSITORY_ROOT/packaging/public_bundle_audit.py" \
	--ipa "$IPA" \
	--manifest "$MANIFEST" \
	--json-report "$ARTIFACT_DIR/public-ipa-audit.json" \
	--markdown-report "$ARTIFACT_DIR/public-ipa-audit.md"

shasum -a 256 "$IPA" > "$ARTIFACT_DIR/NUKE HOUR-PublicClean.ipa.sha256"
printf 'PublicClean IPA: %s\n' "$IPA"
