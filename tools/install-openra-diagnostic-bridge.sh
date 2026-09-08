#!/bin/zsh
set -eu
root="$(cd "$(dirname "$0")/.." && pwd)"
label="com.openra.diagnostic-bridge"
agent="$HOME/Library/LaunchAgents/$label.plist"
mkdir -p "${agent:h}" "$root/.diagnostics"
sed -e "s|__BRIDGE__|$root/tools/openra-diagnostic-bridge|g" \
	-e "s|__LOG__|$root/.diagnostics/bridge.log|g" \
	"$root/tools/com.openra.diagnostic-bridge.plist.template" > "$agent"
chmod +x "$root/tools/openra-diagnostic-bridge"
launchctl bootout "gui/$UID/$label" 2>/dev/null || true
launchctl bootstrap "gui/$UID" "$agent"
echo "Installed $agent"
