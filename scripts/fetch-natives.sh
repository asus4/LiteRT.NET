#!/usr/bin/env bash
# Populates the per-RID native payloads (runtimes/<rid>/native, .gitignore'd) for the
# LiteRT.NET packages (core -> src/LiteRT, LM -> src/LiteRT.LM, GPU -> src/LiteRT.Gpu.*),
# from three best-effort sources (missing pieces warn, not fatal):
#   - LiteRT-LM/prebuilt/<platform>/  desktop+iOS core, GPU accelerators, LM samplers, Gemma plugin
#   - $LITERT_LM_OUT (default: out/)  locally built libLiteRtLmC (+ Gemma) for the host RID
#   - official LiteRT SDK             core for RIDs prebuilt lacks (e.g. Android)
# Run before `dotnet pack`/`dotnet run`.
#
# Env: LITERT_LM_DIR (LiteRT-LM checkout, default ../LiteRT-LM), LITERT_LM_OUT (LM build
# output, default <repo>/out), LITERT_RIDS (RIDs to populate, default all).
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LITERT_LM_DIR="${LITERT_LM_DIR:-$(cd "$REPO_DIR/.." && pwd)/LiteRT-LM}"
LITERT_LM_OUT="${LITERT_LM_OUT:-$REPO_DIR/out}"
CORE_SDK_BASE="https://storage.googleapis.com/litert/binaries/2.1.5"

ALL_RIDS="osx-arm64 linux-x64 linux-arm64 win-x64 android-arm64 android-x64 ios-arm64 iossimulator-arm64"
RIDS="${LITERT_RIDS:-$ALL_RIDS}"

CORE_PKG="$REPO_DIR/src/LiteRT"
GPU_METAL_PKG="$REPO_DIR/src/LiteRT.Gpu.Metal"
GPU_WEBGPU_PKG="$REPO_DIR/src/LiteRT.Gpu.WebGpu"
GPU_OPENCL_PKG="$REPO_DIR/src/LiteRT.Gpu.OpenCl"
LM_PKG="$REPO_DIR/src/LiteRT.LM"

# .NET RID -> LiteRT-LM/prebuilt platform dir name.
prebuilt_platform() {
    case "$1" in
        osx-arm64)            echo "macos_arm64" ;;
        linux-x64)            echo "linux_x86_64" ;;
        linux-arm64)          echo "linux_arm64" ;;
        win-x64)              echo "windows_x86_64" ;;
        android-arm64)        echo "android_arm64" ;;
        android-x64)          echo "android_x86_64" ;;
        ios-arm64)            echo "ios_arm64" ;;
        iossimulator-arm64)   echo "ios_sim_arm64" ;;
        *)                    echo "" ;;
    esac
}

# Classify a native file by basename: core | gpu-metal | gpu-webgpu | gpu-opencl | lm | skip.
# *Accelerator dylibs ship one-per-package so the consumer picks a backend by reference (the
# core's probe order is hardcoded and registers the first one present).
# *Sampler dylibs are skipped on macOS — neither works for LM today, so on-GPU sampling falls
# back to CPU (correct output): the WebGpu sampler prebuilt is stale (missing UpdateConfig et al.
# that current libLiteRtLmC requires), and the Metal sampler only engages under the Metal
# accelerator, which mis-computes LM logits. Route one back to its package when a fixed prebuilt
# appears.
classify() {
    case "$1" in
        libLiteRt.dylib|libLiteRt.so|libLiteRt.dll|LiteRt.dll) echo "core" ;;
        libLiteRtLmC.*|LiteRtLmC.*)                            echo "lm" ;;
        *GemmaModelConstraintProvider*)                        echo "lm" ;;
        *Sampler.*)                                            echo "skip" ;;
        *MetalAccelerator.*)                                   echo "gpu-metal" ;;
        *WebGpuAccelerator.*)                                  echo "gpu-webgpu" ;;
        *OpenClAccelerator.*)                                  echo "gpu-opencl" ;;
        *GpuAccelerator.*)                                     echo "gpu-opencl" ;;
        *)                                                     echo "skip" ;;
    esac
}

# dest_dir <class> <rid> -> package runtimes dir.
dest_dir() {
    case "$1" in
        core)       echo "$CORE_PKG/runtimes/$2/native" ;;
        gpu-metal)  echo "$GPU_METAL_PKG/runtimes/$2/native" ;;
        gpu-webgpu) echo "$GPU_WEBGPU_PKG/runtimes/$2/native" ;;
        gpu-opencl) echo "$GPU_OPENCL_PKG/runtimes/$2/native" ;;
        lm)         echo "$LM_PKG/runtimes/$2/native" ;;
    esac
}

# Copy one shared library into its package (classify -> dest_dir).
place_file() {
    local src="$1" rid="$2"
    local base; base="$(basename "$src")"
    case "$base" in *.lib|BUILD) return 0 ;; esac

    local class; class="$(classify "$base")"
    [ "$class" = "skip" ] && return 0

    # iOS core ships as an xcframework.zip (build_ios_core_xcframework, below), not a loose
    # dylib — a bare .dylib can't be embedded/code-signed on iOS.
    if [ "$class" = "core" ] && { [[ "$rid" == ios-* ]] || [[ "$rid" == iossimulator-* ]]; }; then
        return 0
    fi

    local out; out="$(dest_dir "$class" "$rid")"
    mkdir -p "$out"

    # Core is [DllImport("LiteRt")]; on Windows .NET resolves "LiteRt.dll" (no lib prefix).
    # Plugins keep their as-linked names so dependents' dlopen finds them.
    local name="$base"
    if [ "$class" = "core" ] && [[ "$rid" == win-* ]]; then
        name="LiteRt.dll"
    fi

    cp -f "$src" "$out/$name"
    echo "  + $rid/$class  $name"
}

from_prebuilt() {
    local rid="$1" platform; platform="$(prebuilt_platform "$rid")"
    local dir="$LITERT_LM_DIR/prebuilt/$platform"
    [ -d "$dir" ] || { echo "  (no prebuilt dir: $dir)"; return 0; }
    local f
    for f in "$dir"/*; do
        [ -f "$f" ] && place_file "$f" "$rid"
    done
}

# Locally built LM library (+ Gemma plugin), host RID only.
from_local_lm_out() {
    local rid="$1"
    [ -d "$LITERT_LM_OUT" ] || return 0
    local f
    for f in "$LITERT_LM_OUT"/*; do
        [ -f "$f" ] && place_file "$f" "$rid"
    done
}

# Core for RIDs prebuilt lacks (Android): fetch from the official SDK.
fetch_core_from_sdk() {
    local rid="$1"
    local core_out; core_out="$(dest_dir core "$rid")"
    [ -f "$core_out/libLiteRt.so" ] || [ -f "$core_out/libLiteRt.dylib" ] || \
        [ -f "$core_out/LiteRt.dll" ] && return 0

    local token=""
    case "$rid" in
        android-arm64) token="android_arm64" ;;
        android-x64)   token="android_x86_64" ;;
    esac
    [ -z "$token" ] && return 0

    local url="$CORE_SDK_BASE/$token/libLiteRt.so"
    mkdir -p "$core_out"
    if curl -fsSL "$url" -o "$core_out/libLiteRt.so" 2>/dev/null; then
        echo "  + $rid/core  libLiteRt.so (SDK)"
    else
        rm -f "$core_out/libLiteRt.so"
        echo "  WARNING: core for $rid unavailable (prebuilt has no Android core; SDK fetch failed: $url)" >&2
    fi
}

HOST_RID="osx-arm64"
case "$(uname -s)/$(uname -m)" in
    Darwin/arm64)  HOST_RID="osx-arm64" ;;
    Darwin/x86_64) HOST_RID="osx-x64" ;;
    Linux/x86_64)  HOST_RID="linux-x64" ;;
    Linux/aarch64|Linux/arm64) HOST_RID="linux-arm64" ;;
esac

echo "Fetching natives into src/*/runtimes/ ..."
echo "  LITERT_LM_DIR = $LITERT_LM_DIR"
echo "  LITERT_LM_OUT = $LITERT_LM_OUT"
echo "  HOST_RID      = $HOST_RID"

for rid in $RIDS; do
    echo "[$rid]"
    from_prebuilt "$rid"
    if [ "$rid" = "$HOST_RID" ]; then
        from_local_lm_out "$rid"
    fi
    fetch_core_from_sdk "$rid"
done

# Build the iOS core xcframework.zip when an iOS RID is requested and both prebuilt slices
# exist. macOS-only (needs xcodebuild).
build_ios_core_xcframework() {
    case " $RIDS " in *" ios-arm64 "*|*" iossimulator-arm64 "*) ;; *) return 0 ;; esac
    [ "$(uname -s)" = "Darwin" ] || { echo "  (skip iOS xcframework: not macOS)"; return 0; }
    local dev="$LITERT_LM_DIR/prebuilt/ios_arm64/libLiteRt.dylib"
    local sim="$LITERT_LM_DIR/prebuilt/ios_sim_arm64/libLiteRt.dylib"
    [ -f "$dev" ] && [ -f "$sim" ] || { echo "  (skip iOS xcframework: missing prebuilt dylibs)"; return 0; }
    echo "[ios] building LiteRt.xcframework.zip"
    "$REPO_DIR/scripts/make-ios-xcframework.sh" "$LITERT_LM_DIR/prebuilt" "$CORE_PKG/runtimes/ios/native"
}
build_ios_core_xcframework

echo "Done."
