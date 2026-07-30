#!/usr/bin/env bash
# Regenerates the README screenshots by driving the real app on a private
# virtual display with demo data, so the images always match the shipped UI.
# Produces the main and cookware captures in the default theme plus one
# capture per theme for the README's themes section.
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/display.sh

DOTNET=$(find_dotnet)
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "screenshots: building..."
"$DOTNET" build src/ZFood.App -c Release -v quiet
BIN=src/ZFood.App/bin/Release/net8.0/ZFood

OUT=assets/screenshots
mkdir -p "$OUT"
DATA=$(mktemp -d)
APP_PID=""

cleanup() {
    [ -n "$APP_PID" ] && kill "$APP_PID" 2>/dev/null || true
    stop_virtual_display
    rm -rf "$DATA"
}
trap cleanup EXIT

# Demo cookware; invented generic values.
write_settings() {
    cat > "$DATA/settings.json" <<EOF
{
  "version": 1,
  "theme": "$1",
  "cookware": [
    { "id": "p1", "name": "Big pot", "grams": 640, "pinned": true, "order": 0 },
    { "id": "p2", "name": "Small pot", "grams": 380, "pinned": true, "order": 1 },
    { "id": "p3", "name": "Steel bowl", "grams": 210, "pinned": true, "order": 2 },
    { "id": "p4", "name": "Pan", "grams": 905, "pinned": true, "order": 3 },
    { "id": "p5", "name": "Sieve", "grams": 120, "pinned": false, "order": 4 },
    { "id": "p6", "name": "Cereal bowl", "grams": 285, "pinned": false, "order": 5 }
  ]
}
EOF
}

start_virtual_display

type_into() { xdotool type --window "$WID" --delay 30 "$1"; sleep 0.15; }
key() { xdotool key --window "$WID" "$1"; sleep 0.25; }

# Launches the app in a theme, types the demo values, and captures the window.
capture_theme() {
    local theme=$1 out=$2
    write_settings "$theme"
    rm -f "$DATA/log.jsonl"
    ZFOOD_DATA_DIR="$DATA" "$BIN" &
    APP_PID=$!
    WID=$(wait_for_window)
    xdotool windowfocus --sync "$WID"
    sleep 0.8

    # Portion: a granola label, 56 g = 250 cal, 128 g eaten.
    key alt+p; type_into "56"; key Tab; type_into "250"; key Tab; type_into "128"
    # Scale: Steel bowl holds 400 g of berries; the big pot holds the stew.
    key ctrl+3; type_into "610"
    key ctrl+1; type_into "1440"
    # Recipe weight; Enter commits and copies the water delta.
    key alt+r; type_into "1000"; key Return
    sleep 0.6

    capture_window "$WID" "$out"
    echo "screenshots: captured $out"
}

close_app() {
    [ -n "$APP_PID" ] && kill "$APP_PID" 2>/dev/null || true
    APP_PID=""
    sleep 0.5
}

# One capture per theme.
for theme in juicebar limonata matcha berrymilk porcelain aurora glossy; do
    capture_theme "$theme" "$OUT/theme-$theme.png"
    close_app
done

# The default theme's capture doubles as the README hero image, and the
# cookware editor is captured behind the gear icon at the bottom right.
capture_theme juicebar "$OUT/main.png"
eval "$(xdotool getwindowgeometry --shell "$WID")"
xdotool mousemove --window "$WID" $((WIDTH - 50)) $((HEIGHT - 27)) click 1
sleep 1.2
CWID=$(xdotool search --onlyvisible --name '^Cookware$' | head -1)
if [ -n "$CWID" ]; then
    capture_window "$CWID" "$OUT/cookware.png"
    echo "screenshots: captured $OUT/cookware.png"
else
    echo "error: cookware window did not open" >&2
    exit 1
fi
close_app

for png in "$OUT"/*.png; do
    identify -format "screenshots: $png %wx%h\n" "$png"
done
echo "screenshots: done"
