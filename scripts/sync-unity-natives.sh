#!/usr/bin/env bash
# Copies native libraries from src/<package>/runtimes/<rid>/native into unity/<package>/Plugins/<platform>.
# Run scripts/fetch-natives.sh first (the LM C library also needs scripts/litert-lm-c/build.sh).
# Only one GPU accelerator ships per platform — the core registers the first probe hit
# (see docs/gpu-accelerator-probe-order.md), so extra backends would be dead weight.
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$REPO_DIR/src"
DST="$REPO_DIR/unity"

# src-relative-file | unity-package-relative-destination
MAP=(
    "LiteRT/runtimes/osx-arm64/native/libLiteRt.dylib|LiteRT/Plugins/macOS/libLiteRt.dylib"
    "LiteRT/runtimes/win-x64/native/LiteRt.dll|LiteRT/Plugins/Windows/x86_64/LiteRt.dll"
    "LiteRT/runtimes/linux-x64/native/libLiteRt.so|LiteRT/Plugins/Linux/x86_64/libLiteRt.so"
    "LiteRT/runtimes/android-arm64/native/libLiteRt.so|LiteRT/Plugins/Android/arm64-v8a/libLiteRt.so"
    "LiteRT/runtimes/android-x64/native/libLiteRt.so|LiteRT/Plugins/Android/x86_64/libLiteRt.so"
    "LiteRT/runtimes/ios/native/LiteRt.xcframework.zip|LiteRT/Plugins/iOS/LiteRt.xcframework"
    "LiteRT.Gpu.Metal/runtimes/osx-arm64/native/libLiteRtMetalAccelerator.dylib|LiteRT/Plugins/macOS/libLiteRtMetalAccelerator.dylib"
    "LiteRT.Gpu.OpenCl/runtimes/android-arm64/native/libLiteRtGpuAccelerator.so|LiteRT/Plugins/Android/arm64-v8a/libLiteRtGpuAccelerator.so"
    "LiteRT.Gpu.OpenCl/runtimes/android-x64/native/libLiteRtGpuAccelerator.so|LiteRT/Plugins/Android/x86_64/libLiteRtGpuAccelerator.so"
    "LiteRT.LM/runtimes/osx-arm64/native/libLiteRtLmC.dylib|LiteRT.LM/Plugins/macOS/libLiteRtLmC.dylib"
    "LiteRT.LM/runtimes/osx-arm64/native/libGemmaModelConstraintProvider.dylib|LiteRT.LM/Plugins/macOS/libGemmaModelConstraintProvider.dylib"
    "LiteRT.LM/runtimes/android-arm64/native/libLiteRtLmC.so|LiteRT.LM/Plugins/Android/arm64-v8a/libLiteRtLmC.so"
    "LiteRT.LM/runtimes/android-arm64/native/libGemmaModelConstraintProvider.so|LiteRT.LM/Plugins/Android/arm64-v8a/libGemmaModelConstraintProvider.so"
    "LiteRT.LM/runtimes/android-x64/native/libLiteRtLmC.so|LiteRT.LM/Plugins/Android/x86_64/libLiteRtLmC.so"
    "LiteRT.LM/runtimes/android-x64/native/libGemmaModelConstraintProvider.so|LiteRT.LM/Plugins/Android/x86_64/libGemmaModelConstraintProvider.so"
    "LiteRT.LM/runtimes/ios/native/LiteRtLmC.xcframework.zip|LiteRT.LM/Plugins/iOS/LiteRtLmC.xcframework"
    "LiteRT.LM/runtimes/ios/native/GemmaModelConstraintProvider.xcframework.zip|LiteRT.LM/Plugins/iOS/GemmaModelConstraintProvider.xcframework"
)

echo "Syncing natives -> $DST"
copied=0
for entry in "${MAP[@]}"; do
    src="$SRC/${entry%%|*}"
    dst="$DST/${entry##*|}"
    if [ -f "$src" ]; then
        mkdir -p "$(dirname "$dst")"
        case "$dst" in
            *.xcframework)
                # Unity imports the unzipped .xcframework as a plugin (embedded via the committed .meta).
                rm -rf "$dst"
                unzip -q "$src" -d "$(dirname "$dst")"
                [ -d "$dst" ] || { echo "ERROR: $src did not contain $(basename "$dst") at its root" >&2; exit 1; }
                ;;
            *)
                cp -f "$src" "$dst"
                ;;
        esac
        echo "  + ${entry##*|}"
        copied=$((copied + 1))
    else
        echo "  (missing, skipped) ${entry%%|*}"
    fi
done

echo "Done ($copied file(s) copied)."
