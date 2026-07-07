#!/usr/bin/env bash
# Populates src/*/runtimes/<rid>/native (best-effort) from the official SDK, LiteRT-LM/prebuilt/,
# and locally built LM output. Run before `dotnet pack`/`dotnet run`.
# Env: LITERT_LM_DIR (default ../LiteRT-LM), LITERT_LM_OUT (default <repo>/out), LITERT_RIDS.
# Core must come from the SDK, not LiteRT-LM/prebuilt (newer, incompatible ABI); bump CORE_SDK_BASE only with a bindings review.
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

# classify <basename> -> core | gpu-metal | gpu-webgpu | gpu-opencl | lm | skip.
# Samplers skipped on macOS: the WebGpu prebuilt is stale and the Metal one mis-computes LM logits.
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

dest_dir() {
    case "$1" in
        core)       echo "$CORE_PKG/runtimes/$2/native" ;;
        gpu-metal)  echo "$GPU_METAL_PKG/runtimes/$2/native" ;;
        gpu-webgpu) echo "$GPU_WEBGPU_PKG/runtimes/$2/native" ;;
        gpu-opencl) echo "$GPU_OPENCL_PKG/runtimes/$2/native" ;;
        lm)         echo "$LM_PKG/runtimes/$2/native" ;;
    esac
}

place_file() {
    local src="$1" rid="$2"
    local base; base="$(basename "$src")"
    case "$base" in *.lib|BUILD) return 0 ;; esac

    local class; class="$(classify "$base")"
    [ "$class" = "skip" ] && return 0

    # iOS ships xcframework.zips (see below), not loose dylibs — a bare .dylib can't be embedded/signed.
    if { [ "$class" = "core" ] || [ "$class" = "lm" ]; } && \
        { [[ "$rid" == ios-* ]] || [[ "$rid" == iossimulator-* ]]; }; then
        return 0
    fi

    local out; out="$(dest_dir "$class" "$rid")"
    mkdir -p "$out"

    # Core is [DllImport("LiteRt")]; Windows resolves "LiteRt.dll" (no lib prefix).
    local name="$base"
    if [ "$class" = "core" ] && [[ "$rid" == win-* ]]; then
        name="LiteRt.dll"
    fi

    cp -f "$src" "$out/$name"
    echo "  + $rid/$class  $name"
}

# LM payloads for every RID; GPU accelerators Android-only (the SDK has none for Android). Never core: ABI mismatch.
from_prebuilt() {
    local rid="$1" platform; platform="$(prebuilt_platform "$rid")"
    local dir="$LITERT_LM_DIR/prebuilt/$platform"
    [ -d "$dir" ] || { echo "  (no prebuilt dir: $dir)"; return 0; }
    local f base class
    for f in "$dir"/*; do
        [ -f "$f" ] || continue
        base="$(basename "$f")"
        class="$(classify "$base")"
        case "$class" in
            lm) ;;
            gpu-*) [[ "$rid" == android-* ]] || continue ;;
            *) continue ;;
        esac
        place_file "$f" "$rid"
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

# Cross-built LM library from out/<rid> subdirs (out/ios handled separately below).
from_local_lm_out_rid() {
    local rid="$1"
    local dir="$LITERT_LM_OUT/$rid"
    [ -d "$dir" ] || return 0
    local f
    for f in "$dir"/*; do
        [ -f "$f" ] && place_file "$f" "$rid"
    done
}

# .NET RID -> SDK bucket platform token.
sdk_platform() {
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

# sdk_fetch_file <rid> <sdk-file> — skips if already present so re-runs stay offline-friendly.
sdk_fetch_file() {
    local rid="$1" file="$2"
    local token; token="$(sdk_platform "$rid")"
    [ -n "$token" ] || return 0

    local class; class="$(classify "$file")"
    local out; out="$(dest_dir "$class" "$rid")"
    local name="$file"
    if [ "$class" = "core" ] && [[ "$rid" == win-* ]]; then
        name="LiteRt.dll"
    fi
    [ -f "$out/$name" ] && return 0

    local url="$CORE_SDK_BASE/$token/$file"
    mkdir -p "$out"
    if curl -fsSL "$url" -o "$out/$name" 2>/dev/null; then
        echo "  + $rid/$class  $name (SDK)"
    else
        rm -f "$out/$name"
        echo "  WARNING: $file for $rid unavailable (SDK fetch failed: $url)" >&2
    fi
}

# Core (+ matching GPU accelerator) from the SDK; iOS core is wrapped by build_ios_core_xcframework instead.
fetch_from_sdk() {
    local rid="$1"
    case "$rid" in
        osx-arm64)
            sdk_fetch_file "$rid" "libLiteRt.dylib"
            sdk_fetch_file "$rid" "libLiteRtMetalAccelerator.dylib"
            ;;
        linux-x64|linux-arm64)
            sdk_fetch_file "$rid" "libLiteRt.so"
            sdk_fetch_file "$rid" "libLiteRtWebGpuAccelerator.so"
            ;;
        win-x64)
            sdk_fetch_file "$rid" "libLiteRt.dll"
            sdk_fetch_file "$rid" "libLiteRtWebGpuAccelerator.dll"
            ;;
        android-arm64|android-x64)
            sdk_fetch_file "$rid" "libLiteRt.so"
            ;;
        ios-arm64|iossimulator-arm64)
            sdk_fetch_file "$rid" "libLiteRtMetalAccelerator.dylib"
            ;;
    esac
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
    from_local_lm_out_rid "$rid"
    fetch_from_sdk "$rid"
done

# Wrap the SDK dylibs into LiteRt.xcframework.zip when an iOS RID is requested; macOS-only (needs xcodebuild).
build_ios_core_xcframework() {
    case " $RIDS " in *" ios-arm64 "*|*" iossimulator-arm64 "*) ;; *) return 0 ;; esac
    [ "$(uname -s)" = "Darwin" ] || { echo "  (skip iOS xcframework: not macOS)"; return 0; }

    local stage="$CORE_PKG/runtimes/.sdk-ios"
    local token
    for token in ios_arm64 ios_sim_arm64; do
        if [ ! -f "$stage/$token/libLiteRt.dylib" ]; then
            mkdir -p "$stage/$token"
            if ! curl -fsSL "$CORE_SDK_BASE/$token/libLiteRt.dylib" \
                -o "$stage/$token/libLiteRt.dylib" 2>/dev/null; then
                rm -f "$stage/$token/libLiteRt.dylib"
                echo "  (skip iOS xcframework: SDK fetch failed for $token)" >&2
                return 0
            fi
        fi
    done
    echo "[ios] building LiteRt.xcframework.zip (SDK dylibs)"
    "$REPO_DIR/scripts/make-ios-xcframework.sh" "$stage" "$CORE_PKG/runtimes/ios/native"
}
build_ios_core_xcframework

# LM iOS xcframework.zips come from scripts/litert-lm-c/build.sh <src> out/ios ios.
place_lm_ios_xcframeworks() {
    case " $RIDS " in *" ios-arm64 "*|*" iossimulator-arm64 "*) ;; *) return 0 ;; esac
    local dir="$LITERT_LM_OUT/ios"
    [ -d "$dir" ] || { echo "  (no LM iOS zips: $dir — run scripts/litert-lm-c/build.sh <src> out/ios ios)"; return 0; }
    local out="$LM_PKG/runtimes/ios/native"
    local f placed=0
    for f in "$dir"/*.xcframework.zip; do
        [ -f "$f" ] || continue
        mkdir -p "$out"
        cp -f "$f" "$out/$(basename "$f")"
        echo "  + ios/lm  $(basename "$f")"
        placed=1
    done
    [ "$placed" = 1 ] || echo "  (no *.xcframework.zip in $dir)"
}
place_lm_ios_xcframeworks

echo "Done."
