#!/bin/sh

set -eu

SERVER_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SUPPORT_DIR=${SupportDir:-"${SERVER_ROOT}/support"}
STATUS_FILE=${StatusFile:-"status/server-status.json"}
case "${STATUS_FILE}" in
	/*) STATUS_PATH=${STATUS_FILE} ;;
	*) STATUS_PATH=${SUPPORT_DIR}/${STATUS_FILE} ;;
esac
STATUS_PATH=${NUKEHOUR_STATUS_FILE:-"${STATUS_PATH}"}

[ -r "${STATUS_PATH}" ] || exit 1
grep -q '"processAlive":true' "${STATUS_PATH}" || exit 1
grep -q '"ready":true' "${STATUS_PATH}" || exit 1
