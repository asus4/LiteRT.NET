#!/usr/bin/env bash
# Builds libLiteRtLmC, a shared library exposing the LiteRT-LM C API (c/engine.h), by
# dropping a small Bazel package into a LiteRT-LM checkout and building it with linkshared=1.
#
# Usage: build.sh <litert-lm-source-dir> <output-dir>
# Env:   BAZEL (default: bazelisk if present, else bazel)
# Output: <output-dir>/libLiteRtLmC.{so|dylib|dll} (+ .dll import lib on Windows)
set -euo pipefail

SRC_DIR="${1:?usage: build.sh <litert-lm-source-dir> <output-dir>}"
OUT_DIR="${2:?usage: build.sh <litert-lm-source-dir> <output-dir>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

BAZEL="${BAZEL:-$(command -v bazelisk || command -v bazel)}"
mkdir -p "$OUT_DIR"

# Inject the wrapper Bazel package into the checkout.
PKG_DIR="$SRC_DIR/litert_lm_dotnet"
mkdir -p "$PKG_DIR"
cp "$SCRIPT_DIR/BUILD.bazel" "$PKG_DIR/BUILD"

echo "Building //litert_lm_dotnet:LiteRtLmC with $BAZEL ..."
( cd "$SRC_DIR" && "$BAZEL" build //litert_lm_dotnet:LiteRtLmC -c opt )

BIN_DIR="$SRC_DIR/bazel-bin/litert_lm_dotnet"
case "$(uname -s)" in
    Darwin)
        # Bazel emits .dylib or .so depending on the toolchain; ship either as .dylib.
        if [ -f "$BIN_DIR/libLiteRtLmC.dylib" ]; then
            cp "$BIN_DIR/libLiteRtLmC.dylib" "$OUT_DIR/libLiteRtLmC.dylib"
        else
            cp "$BIN_DIR/libLiteRtLmC.so" "$OUT_DIR/libLiteRtLmC.dylib"
        fi
        ;;
    Linux)
        cp "$BIN_DIR/libLiteRtLmC.so" "$OUT_DIR/libLiteRtLmC.so"
        ;;
    MINGW*|MSYS*|CYGWIN*)
        cp "$BIN_DIR/LiteRtLmC.dll" "$OUT_DIR/LiteRtLmC.dll"
        [ -f "$BIN_DIR/LiteRtLmC.if.lib" ] && cp "$BIN_DIR/LiteRtLmC.if.lib" "$OUT_DIR/" || true
        ;;
    *)
        echo "Unknown platform: $(uname -s)" >&2
        exit 1
        ;;
esac

# libLiteRtLmC load-time depends on @rpath/libGemmaModelConstraintProvider.* (rpath includes
# @loader_path), so the plugin must sit beside it. We don't build it — copy the prebuilt.
case "$(uname -s)" in
    Darwin)  PLUGIN_PLATFORM="macos_arm64"; PLUGIN_EXT="dylib" ;;
    Linux)   PLUGIN_EXT="so"
             case "$(uname -m)" in
                 aarch64|arm64) PLUGIN_PLATFORM="linux_arm64" ;;
                 *)             PLUGIN_PLATFORM="linux_x86_64" ;;
             esac ;;
    MINGW*|MSYS*|CYGWIN*) PLUGIN_PLATFORM="windows_x86_64"; PLUGIN_EXT="dll" ;;
esac
PLUGIN="$SRC_DIR/prebuilt/$PLUGIN_PLATFORM/libGemmaModelConstraintProvider.$PLUGIN_EXT"
if [ -f "$PLUGIN" ]; then
    cp "$PLUGIN" "$OUT_DIR/"
else
    echo "WARNING: constraint-provider plugin not found at $PLUGIN" >&2
    echo "         libLiteRtLmC may fail to load without it." >&2
fi

echo "Output written to $OUT_DIR:"
ls -la "$OUT_DIR"
