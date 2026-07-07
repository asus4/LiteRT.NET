#!/usr/bin/env bash
# Copies the native libraries from the per-RID layout (src/<package>/runtimes/<rid>/native)
# into the Unity packages' Plugins/ trees (unity/<package>/Plugins/<platform>). The .meta files
# there are committed (stable GUIDs + platform settings); only the .gitignore'd binaries are
# copied. Run `scripts/fetch-natives.sh` first to populate the source runtimes; the LM C library
# additionally needs `scripts/litert-lm-c/build.sh`. CI runs these before `upm pack`. iOS core
# ships as LiteRt.xcframework.zip (embedded by LiteRtPostprocessBuild).
#
# GPU accelerators: only one plugin per platform is shipped — the core probes a hardcoded,
# ordered basename list and registers the first hit (see docs/gpu-accelerator-probe-order.md).
# macOS gets Metal; Android gets the multi-backend GpuAccelerator (OpenCL+WebGPU+Vulkan),
# which sits at probe index 0, so shipping the OpenCL-only dylib as well would be dead weight.
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
    "LiteRT/runtimes/ios/native/LiteRt.xcframework.zip|LiteRT/Plugins/iOS/LiteRt.xcframework.zip"
    "LiteRT.Gpu.Metal/runtimes/osx-arm64/native/libLiteRtMetalAccelerator.dylib|LiteRT/Plugins/macOS/libLiteRtMetalAccelerator.dylib"
    "LiteRT.Gpu.OpenCl/runtimes/android-arm64/native/libLiteRtGpuAccelerator.so|LiteRT/Plugins/Android/arm64-v8a/libLiteRtGpuAccelerator.so"
    "LiteRT.Gpu.OpenCl/runtimes/android-x64/native/libLiteRtGpuAccelerator.so|LiteRT/Plugins/Android/x86_64/libLiteRtGpuAccelerator.so"
    "LiteRT.LM/runtimes/osx-arm64/native/libLiteRtLmC.dylib|LiteRT.LM/Plugins/macOS/libLiteRtLmC.dylib"
    "LiteRT.LM/runtimes/osx-arm64/native/libGemmaModelConstraintProvider.dylib|LiteRT.LM/Plugins/macOS/libGemmaModelConstraintProvider.dylib"
)

echo "Syncing natives -> $DST"
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
