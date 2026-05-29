#!/usr/bin/env bash
# Populates the per-RID native payloads for the LiteRT.NET Native packages.
#
# Layout produced (relative to repo root):
#   src/LiteRT.Native/runtimes/<rid>/native/      core libLiteRt
#   src/LiteRT.Gpu.Native/runtimes/<rid>/native/  GPU accelerators + samplers
#   src/LiteRT.LM.Native/runtimes/<rid>/native/   libLiteRtLmC + Gemma constraint plugin
#
# These directories are .gitignore'd; binaries ship only inside NuGet packages
# and on GitHub Release pages. Run this before `dotnet pack`/`dotnet run`.
#
# Sources (best-effort; missing pieces are warned, not fatal):
#   - LiteRT-LM/prebuilt/<platform>/  : desktop+iOS core, GPU accel/samplers, Gemma plugin
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
GPU_PKG="$REPO_DIR/src/LiteRT.Gpu.Native"
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

# Classify a native file by basename: core | gpu | lm | skip
classify() {
    case "$1" in
        libLiteRt.dylib|libLiteRt.so|libLiteRt.dll|LiteRt.dll) echo "core" ;;
        libLiteRtLmC.*|LiteRtLmC.*)                            echo "lm" ;;
        *GemmaModelConstraintProvider*)                        echo "lm" ;;
        *Accelerator.*|*Sampler.*)                             echo "gpu" ;;
        *)                                                     echo "skip" ;;
    esac
}

# Destination dir for a class, given the rid.
dest_dir() {
    case "$1" in
        core) echo "$CORE_PKG/runtimes/$2/native" ;;
        gpu)  echo "$GPU_PKG/runtimes/$2/native" ;;
        lm)   echo "$LM_PKG/runtimes/$2/native" ;;
    esac
}

# Copy a single shared library into the right package, applying OS-default
# P/Invoke naming for the core entry-point lib on Windows (lib-prefix stripped).
place_file() {
    local src="$1" rid="$2"
    local base; base="$(basename "$src")"
    case "$base" in *.lib|BUILD) return 0 ;; esac

    local class; class="$(classify "$base")"
    [ "$class" = "skip" ] && return 0

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

echo "Done."
