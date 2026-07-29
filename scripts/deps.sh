#!/usr/bin/env bash
# Checks for the system tools the make targets rely on and installs anything
# missing with apt-get (Debian/Ubuntu package names), prompting via sudo.
set -euo pipefail

# Tool -> package, derived from what the targets actually invoke:
#   make setup:                    curl (downloads the .NET SDK installer)
#   make smoke, make screenshots:  Xvfb, xdotool, ImageMagick import/identify
#   make icons:                    ImageMagick convert/identify
TOOLS=(curl Xvfb xdotool import convert identify)

package_for() {
    case "$1" in
        curl) echo curl ;;
        Xvfb) echo xvfb ;;
        xdotool) echo xdotool ;;
        import | convert | identify) echo imagemagick ;;
    esac
}

missing=()
for tool in "${TOOLS[@]}"; do
    if command -v "$tool" >/dev/null 2>&1; then
        echo "deps: found $tool"
    else
        echo "deps: missing $tool (package: $(package_for "$tool"))"
        missing+=("$(package_for "$tool")")
    fi
done

if [ "${#missing[@]}" -eq 0 ]; then
    echo "deps: nothing missing; all required tools are installed"
    exit 0
fi

readarray -t packages < <(printf '%s\n' "${missing[@]}" | sort -u)
echo "deps: will run: sudo apt-get install -y ${packages[*]}"
sudo apt-get install -y "${packages[@]}"
