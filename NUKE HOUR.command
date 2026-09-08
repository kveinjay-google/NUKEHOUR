#!/bin/sh
cd "$(dirname "$0")"

export DOTNET_ROLL_FORWARD=LatestMajor
export PATH="/usr/local/share/dotnet:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:${PATH}"

PYTHON="/opt/homebrew/bin/python3"
if ! "$PYTHON" -c 'import tkinter, PIL' >/dev/null 2>&1; then
	osascript -e 'display alert "NUKE HOUR Launcher / NUKE HOUR 启动器" message "Homebrew Python Tk is required: brew install python-tk@3.14" & return & "需要安装 Homebrew Python Tk：brew install python-tk@3.14" as critical'
	exit 1
fi

exec "$PYTHON" launcher.py
