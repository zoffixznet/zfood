#!/usr/bin/env bash
# End-to-end smoke test: build the app, launch the real binary on a private
# virtual display, verify the window appears and the diagnostics log records
# startup, capture a screenshot, close the window, and assert a clean exit
# with a shutdown record. Safe to run with or without a desktop session.
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/display.sh

DOTNET=$(find_dotnet)
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "smoke: building..."
"$DOTNET" build src/ZFood.App -c Release -v quiet
BIN=src/ZFood.App/bin/Release/net8.0/ZFood
[ -x "$BIN" ] || { echo "error: $BIN not built" >&2; exit 1; }

DATA=$(mktemp -d)
OUT=$(mktemp -d)
APP_PID=""

cleanup() {
    [ -n "$APP_PID" ] && kill "$APP_PID" 2>/dev/null || true
    stop_virtual_display
    rm -rf "$DATA" "$OUT"
}
trap cleanup EXIT

start_virtual_display
echo "smoke: launching on $DISPLAY"
ZFOOD_DATA_DIR="$DATA" "$BIN" &
APP_PID=$!

WID=$(wait_for_window)
echo "smoke: window appeared (id $WID)"

grep -q "startup" "$DATA/diagnostics.log" || { echo "FAIL: no startup record in diagnostics log" >&2; exit 1; }
echo "smoke: diagnostics log records startup"

import -window "$WID" "$OUT/smoke.png"
SIZE=$(identify -format '%wx%h' "$OUT/smoke.png")
echo "smoke: captured screenshot ($SIZE)"
case "$SIZE" in
    *x*) ;;
    *) echo "FAIL: bad screenshot" >&2; exit 1 ;;
esac

xdotool windowclose "$WID"
for _ in $(seq 1 100); do
    kill -0 "$APP_PID" 2>/dev/null || break
    sleep 0.1
done
if kill -0 "$APP_PID" 2>/dev/null; then
    echo "FAIL: app did not exit after window close" >&2
    exit 1
fi
wait "$APP_PID" || { echo "FAIL: app exited with an error" >&2; exit 1; }
APP_PID=""
echo "smoke: clean exit"

grep -q "shutdown" "$DATA/diagnostics.log" || { echo "FAIL: no shutdown record in diagnostics log" >&2; exit 1; }
echo "smoke: diagnostics log records shutdown"

echo "smoke: PASS"
