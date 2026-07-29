#!/usr/bin/env bash
# Regenerates all raster icons from the SVG source and verifies the output.
#
# Produces:
#   assets/icon/zfood-<size>.png   for 16, 24, 32, 48, 64, 128, 256, 512
#   src/ZFood.App/Assets/zfood.png (window icon)
#   src/ZFood.App/Assets/zfood.ico (Windows executable icon, multi-size)
set -euo pipefail
cd "$(dirname "$0")/.."

SVG=assets/icon/zfood.svg
OUT=assets/icon
APP_ASSETS=src/ZFood.App/Assets
SIZES=(16 24 32 48 64 128 256 512)

command -v convert >/dev/null || { echo "error: ImageMagick 'convert' not found" >&2; exit 1; }

mkdir -p "$OUT" "$APP_ASSETS"

for size in "${SIZES[@]}"; do
    png="$OUT/zfood-$size.png"
    convert -background none -density 384 "$SVG" -resize "${size}x${size}" "$png"
done

# Multi-size .ico for the Windows executable (256 is the ICO maximum).
convert "$OUT"/zfood-{16,24,32,48,64,128,256}.png "$APP_ASSETS/zfood.ico"

# Window icon used at runtime.
cp "$OUT/zfood-256.png" "$APP_ASSETS/zfood.png"

# Verify every output has the expected geometry and is not a blank render.
fail=0
for size in "${SIZES[@]}"; do
    png="$OUT/zfood-$size.png"
    dims=$(identify -format '%wx%h' "$png")
    [ "$dims" = "${size}x${size}" ] || { echo "error: $png is $dims, expected ${size}x${size}" >&2; fail=1; }
    colors=$(identify -format '%k' "$png")
    [ "$colors" -ge 3 ] || { echo "error: $png has only $colors colors, render looks blank" >&2; fail=1; }
done
icodims=$(identify -format '%wx%h ' "$APP_ASSETS/zfood.ico")
echo "icon sizes in zfood.ico: $icodims"
case "$icodims" in
    *256x256*) ;;
    *) echo "error: zfood.ico is missing the 256x256 image" >&2; fail=1 ;;
esac

[ "$fail" -eq 0 ] && echo "icons OK"
exit "$fail"
