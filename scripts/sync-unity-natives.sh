#!/usr/bin/env bash
# Copies the core LiteRT native libraries from the NuGet per-RID layout
# (src/LiteRT.Native/runtimes/<rid>/native) into the Unity package's Plugins/ tree
# (unity/LiteRT/Plugins/<platform>), where Unity imports them as native plugins.
#
# The .meta files under unity/LiteRT/Plugins are committed (stable GUIDs + platform
# settings); only the binaries are copied here. Binaries stay .gitignore'd and are
# populated by this script — run `scripts/fetch-natives.sh` first to fill the source
# runtimes, then this. CI runs both before `upm pack`.
#
# iOS ships as LiteRt.xcframework.zip (a loose .dylib can't be embedded/code-signed);
# it is imported as a plain asset and unzipped + embedded by LiteRtPostprocessBuild.
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$REPO_DIR/src/LiteRT.Native/runtimes"
DST="$REPO_DIR/unity/LiteRT/Plugins"

# rid:src-relative-file -> dst-relative-file
MAP=(
    "osx-arm64/native/libLiteRt.dylib|macOS/libLiteRt.dylib"
    "win-x64/native/LiteRt.dll|Windows/x86_64/LiteRt.dll"
    "linux-x64/native/libLiteRt.so|Linux/x86_64/libLiteRt.so"
    "android-arm64/native/libLiteRt.so|Android/arm64-v8a/libLiteRt.so"
    "android-x64/native/libLiteRt.so|Android/x86_64/libLiteRt.so"
    "ios/native/LiteRt.xcframework.zip|iOS/LiteRt.xcframework.zip"
)

echo "Syncing core natives -> $DST"
copied=0
for entry in "${MAP[@]}"; do
    src="$SRC/${entry%%|*}"
    dst="$DST/${entry##*|}"
    if [ -f "$src" ]; then
        mkdir -p "$(dirname "$dst")"
        cp -f "$src" "$dst"
        echo "  + ${entry##*|}"
        copied=$((copied + 1))
    else
        echo "  (missing, skipped) ${entry%%|*}"
    fi
done

echo "Done ($copied file(s) copied)."
