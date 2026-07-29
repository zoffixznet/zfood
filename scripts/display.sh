#!/usr/bin/env bash
# Shared helpers for driving the app on a private virtual X display.
# Sourced by smoke.sh and screenshots.sh; never touches the user's session.

VDISPLAY_PID=""

# Starts Xvfb on a free display number and exports DISPLAY.
start_virtual_display() {
    command -v Xvfb >/dev/null || { echo "error: Xvfb not found" >&2; exit 1; }
    command -v xdotool >/dev/null || { echo "error: xdotool not found" >&2; exit 1; }
    command -v import >/dev/null || { echo "error: ImageMagick 'import' not found" >&2; exit 1; }

    local fdfile
    fdfile=$(mktemp)
    Xvfb -displayfd 3 -screen 0 1600x1200x24 3>"$fdfile" &
    VDISPLAY_PID=$!
    for _ in $(seq 1 50); do
        [ -s "$fdfile" ] && break
        sleep 0.1
    done
    [ -s "$fdfile" ] || { echo "error: Xvfb did not start" >&2; exit 1; }
    export DISPLAY=":$(cat "$fdfile")"
    rm -f "$fdfile"
}

stop_virtual_display() {
    [ -n "$VDISPLAY_PID" ] && kill "$VDISPLAY_PID" 2>/dev/null || true
}

# Waits for the ZFood main window and echoes its X window id.
wait_for_window() {
    local wid=""
    for _ in $(seq 1 100); do
        wid=$(xdotool search --name '^ZFood$' 2>/dev/null | head -1)
        [ -n "$wid" ] && break
        sleep 0.1
    done
    [ -n "$wid" ] || { echo "error: ZFood window did not appear" >&2; return 1; }
    echo "$wid"
}

# Resolves the dotnet to use, matching the Makefile's logic.
find_dotnet() {
    if command -v dotnet >/dev/null 2>&1; then
        command -v dotnet
    elif [ -x .dotnet/dotnet ]; then
        export DOTNET_ROOT="$PWD/.dotnet"
        echo "$PWD/.dotnet/dotnet"
    else
        echo "error: no dotnet found; run 'make setup' first" >&2
        exit 1
    fi
}
