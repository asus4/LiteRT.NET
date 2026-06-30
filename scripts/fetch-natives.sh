#!/usr/bin/env bash
# Populates the per-RID native payloads for the LiteRT.NET Native packages.
#
# Layout produced (relative to repo root):
#   src/LiteRT.Native/runtimes/<rid>/native/      core libLiteRt
#   src/LiteRT.Gpu.Native/runtimes/<rid>/native/  core GPU accelerators (Metal/WebGpu/OpenCl)
#   src/LiteRT.LM.Native/runtimes/<rid>/native/   libLiteRtLmC + Gemma plugin + LM GPU samplers
#
# These directories are .gitignore'd; binaries ship only inside NuGet packages
# and on GitHub Release pages. Run this before `dotnet pack`/`dotnet run`.
#
# Sources (best-effort; missing pieces are warned, not fatal):
#   - LiteRT-LM/prebuilt/<platform>/  : desktop+iOS core, GPU accelerators, LM samplers, Gemma plugin
#   - $LITERT_LM_OUT (default: out/)  : locally built libLiteRtLmC (+ Gemma) for the host RID
#   - official LiteRT SDK             : core for RIDs not covered by prebuilt (e.g. Android)
#
# Environment:
#   LITERT_LM_DIR  Path to a LiteRT-LM checkout (default: ../LiteRT-LM)
#   LITERT_LM_OUT  Path to locally built LM output (default: <repo>/out)
#   LITERT_RIDS    Space-separated RIDs to populate (default: all known RIDs)
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LITERT_LM_DIR="${LITERT_LM_DIR:-$(cd "$REPO_DIR/.." && pwd)/LiteRT-LM}"
LITERT_LM_OUT="${LITERT_LM_OUT:-$REPO_DIR/out}"
CORE_SDK_BASE="https://storage.googleapis.com/litert/binaries/2.1.5"

ALL_RIDS="osx-arm64 linux-x64 linux-arm64 win-x64 android-arm64 android-x64 ios-arm64 iossimulator-arm64"
RIDS="${LITERT_RIDS:-$ALL_RIDS}"

CORE_PKG="$REPO_DIR/src/LiteRT.Native"
GPU_METAL_PKG="$REPO_DIR/src/LiteRT.Gpu.Metal.Native"
GPU_WEBGPU_PKG="$REPO_DIR/src/LiteRT.Gpu.WebGpu.Native"
GPU_OPENCL_PKG="$REPO_DIR/src/LiteRT.Gpu.OpenCl.Native"
LM_PKG="$REPO_DIR/src/LiteRT.LM.Native"

# Map a .NET RID to the LiteRT-LM/prebuilt platform directory name.
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

# Classify a native file by basename: core | gpu-metal | gpu-webgpu | gpu-opencl | lm | skip
#
# *Accelerator are core LiteRT GPU delegates (used by LiteRT.Managed for .tflite GPU
# inference AND by LiteRT-LM). They ship in per-backend packages so a consumer can pick
# exactly one: the registry's accelerator probe order is hardcoded in the prebuilt lib
# and registers the FIRST present, so "which backend" == "which dylib is in bin".
#
# *Sampler (libLiteRtTopK{Metal,WebGpu,OpenCl}Sampler) are LiteRT-LM decode-time plugins
# that the LM sampler factory dlopens by bare leaf name. We deliberately DO NOT ship them on
# macOS; on-GPU sampling falls back to CPU sampling (correct output) instead. Verified reasons
# (LiteRT-LM/prebuilt/macos_arm64, checked with `nm -gU`):
#   - WebGpu sampler: the recommended LM GPU backend, but the prebuilt dylib is STALE — it only
#     exports Create/Destroy/SampleToIdAndScoreBuffer and is missing UpdateConfig (plus
#     CanHandleInput/HandlesInput/SetInputTensorsAndInferenceFunc) that the sampler_factory in
#     the libLiteRtLmC we build now requires, so dlsym fails and the factory falls back to CPU.
#   - Metal sampler: ABI-complete (loads fine), but the Metal accelerator mis-computes LM logits
#     (garbled output), so its only activation path is unusable for LM anyway.
# Net: neither yields correct on-GPU sampling on macOS today. Re-enable by routing the relevant
# sampler to its backend package below once a refreshed prebuilt (full WebGpu sampler ABI, or a
# fixed Metal accelerator) appears.
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

# Destination dir for a class, given the rid.
dest_dir() {
    case "$1" in
        core)       echo "$CORE_PKG/runtimes/$2/native" ;;
        gpu-metal)  echo "$GPU_METAL_PKG/runtimes/$2/native" ;;
        gpu-webgpu) echo "$GPU_WEBGPU_PKG/runtimes/$2/native" ;;
        gpu-opencl) echo "$GPU_OPENCL_PKG/runtimes/$2/native" ;;
        lm)         echo "$LM_PKG/runtimes/$2/native" ;;
    esac
}

# Copy a single shared library into the right package, applying OS-default
# P/Invoke naming for the core entry-point lib on Windows (lib-prefix stripped).
place_file() {
    local src="$1" rid="$2"
    local base; base="$(basename "$src")"
    case "$base" in *.lib|BUILD) return 0 ;; esac

    # Backends are routed into separate packages (classify -> dest_dir), so a consumer
    # picks the accelerator by referencing one package. macOS ships both Metal and WebGPU
    # prebuilts; they land in their respective packages. (For LM decode the Metal
    # accelerator mis-computes logits, so LM consumers should use LiteRT.Gpu.WebGpu.Native.)
    local class; class="$(classify "$base")"
    [ "$class" = "skip" ] && return 0

    # iOS core ships as an xcframework.zip (built once by build_ios_core_xcframework below),
    # not a bare per-RID dylib — a loose .dylib can't be embedded/code-signed on iOS.
    if [ "$class" = "core" ] && { [[ "$rid" == ios-* ]] || [[ "$rid" == iossimulator-* ]]; }; then
        return 0
    fi

    local out; out="$(dest_dir "$class" "$rid")"
    mkdir -p "$out"

    local name="$base"
    # Core is P/Invoke'd as [DllImport("LiteRt")]; on Windows .NET resolves
    # "LiteRt.dll" (no lib prefix). Plugins keep their as-linked names so the
    # loader / dlopen can find them by the name embedded in their dependents.
    if [ "$class" = "core" ] && [[ "$rid" == win-* ]]; then
        name="LiteRt.dll"
    fi

    cp -f "$src" "$out/$name"
    echo "  + $rid/$class  $name"
}

# Copy everything available from LiteRT-LM/prebuilt/<platform> for a rid.
from_prebuilt() {
    local rid="$1" platform; platform="$(prebuilt_platform "$rid")"
    local dir="$LITERT_LM_DIR/prebuilt/$platform"
    [ -d "$dir" ] || { echo "  (no prebuilt dir: $dir)"; return 0; }
    local f
    for f in "$dir"/*; do
        [ -f "$f" ] && place_file "$f" "$rid"
    done
}

# Copy the locally built LM library (and its Gemma plugin) for the host RID.
from_local_lm_out() {
    local rid="$1"
    [ -d "$LITERT_LM_OUT" ] || return 0
    local f
    for f in "$LITERT_LM_OUT"/*; do
        [ -f "$f" ] && place_file "$f" "$rid"
    done
}

# For RIDs whose core is not in prebuilt (Android), try the official SDK.
fetch_core_from_sdk() {
    local rid="$1"
    local core_out; core_out="$(dest_dir core "$rid")"
    # Already populated from prebuilt? then skip.
    [ -f "$core_out/libLiteRt.so" ] || [ -f "$core_out/libLiteRt.dylib" ] || \
        [ -f "$core_out/LiteRt.dll" ] && return 0

    # SDK platform tokens may differ from prebuilt tokens; adjust if upstream changes.
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

echo "Fetching natives into src/*.Native/runtimes/ ..."
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

# Build the iOS core xcframework.zip (device + simulator) when both prebuilt slices exist
# and an iOS RID was requested. macOS-only (needs xcodebuild); skipped elsewhere.
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
