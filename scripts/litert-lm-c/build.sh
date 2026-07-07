#!/usr/bin/env bash
# Builds libLiteRtLmC, a shared library exposing the LiteRT-LM C API (c/engine.h), by
# dropping a small Bazel package into a LiteRT-LM checkout and building it with linkshared=1.
#
# Usage: build.sh <litert-lm-source-dir> <output-dir> [target]
#   target: host (default)  -> <output-dir>/libLiteRtLmC.{so|dylib|dll} + Gemma provider
#           android_arm64   -> <output-dir>/libLiteRtLmC.so   (needs ANDROID_NDK_HOME, r28b+)
#           android_x86_64  -> <output-dir>/libLiteRtLmC.so
#           ios             -> <output-dir>/{LiteRtLmC,GemmaModelConstraintProvider}.xcframework.zip
#                              (macOS host only; device + simulator arm64 slices)
# Env:   BAZEL (default: bazelisk if present, else bazel)
set -euo pipefail

SRC_DIR="${1:?usage: build.sh <litert-lm-source-dir> <output-dir> [host|android_arm64|android_x86_64|ios]}"
OUT_DIR="${2:?usage: build.sh <litert-lm-source-dir> <output-dir> [host|android_arm64|android_x86_64|ios]}"
TARGET="${3:-host}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

BAZEL="${BAZEL:-$(command -v bazelisk || command -v bazel)}"
mkdir -p "$OUT_DIR"
OUT_DIR="$(cd "$OUT_DIR" && pwd)"  # absolute: zip/copy steps run from other directories

# Inject the wrapper Bazel package into the checkout.
PKG_DIR="$SRC_DIR/litert_lm_dotnet"
mkdir -p "$PKG_DIR"
cp "$SCRIPT_DIR/BUILD.bazel" "$PKG_DIR/BUILD"
cp "$SCRIPT_DIR/Info.plist" "$PKG_DIR/Info.plist"

BIN_DIR="$SRC_DIR/bazel-bin/litert_lm_dotnet"

# copy_prebuilt_provider <prebuilt-platform> <ext>
# libLiteRtLmC load-time depends on @rpath/libGemmaModelConstraintProvider.* (rpath includes
# @loader_path/$ORIGIN), so the plugin must sit beside it. We don't build it — copy the prebuilt.
copy_prebuilt_provider() {
    local plugin="$SRC_DIR/prebuilt/$1/libGemmaModelConstraintProvider.$2"
    if [ -f "$plugin" ]; then
        cp "$plugin" "$OUT_DIR/"
    else
        echo "WARNING: constraint-provider plugin not found at $plugin" >&2
        echo "         libLiteRtLmC may fail to load without it." >&2
    fi
}

build_host() {
    echo "Building //litert_lm_dotnet:LiteRtLmC ($TARGET) with $BAZEL ..."
    ( cd "$SRC_DIR" && "$BAZEL" build //litert_lm_dotnet:LiteRtLmC -c opt )

    case "$(uname -s)" in
        Darwin)
            # Bazel emits .dylib or .so depending on the toolchain; ship either as .dylib.
            if [ -f "$BIN_DIR/libLiteRtLmC.dylib" ]; then
                cp "$BIN_DIR/libLiteRtLmC.dylib" "$OUT_DIR/libLiteRtLmC.dylib"
            else
                cp "$BIN_DIR/libLiteRtLmC.so" "$OUT_DIR/libLiteRtLmC.dylib"
            fi
            copy_prebuilt_provider "macos_arm64" "dylib"
            ;;
        Linux)
            cp "$BIN_DIR/libLiteRtLmC.so" "$OUT_DIR/libLiteRtLmC.so"
            case "$(uname -m)" in
                aarch64|arm64) copy_prebuilt_provider "linux_arm64" "so" ;;
                *)             copy_prebuilt_provider "linux_x86_64" "so" ;;
            esac
            ;;
        MINGW*|MSYS*|CYGWIN*)
            cp "$BIN_DIR/LiteRtLmC.dll" "$OUT_DIR/LiteRtLmC.dll"
            [ -f "$BIN_DIR/LiteRtLmC.if.lib" ] && cp "$BIN_DIR/LiteRtLmC.if.lib" "$OUT_DIR/" || true
            copy_prebuilt_provider "windows_x86_64" "dll"
            ;;
        *)
            echo "Unknown platform: $(uname -s)" >&2
            exit 1
            ;;
    esac
}

build_android() {
    : "${ANDROID_NDK_HOME:?android targets need ANDROID_NDK_HOME (NDK r28b or newer)}"
    echo "Building //litert_lm_dotnet:LiteRtLmC (--config=$TARGET) with $BAZEL ..."
    ( cd "$SRC_DIR" && "$BAZEL" build //litert_lm_dotnet:LiteRtLmC --config="$TARGET" -c opt )
    cp "$BIN_DIR/libLiteRtLmC.so" "$OUT_DIR/libLiteRtLmC.so"

    # Sanity: 16 KB page alignment + no unexpected load-time deps.
    local readelf
    readelf="$(find "$ANDROID_NDK_HOME/toolchains/llvm/prebuilt" -name llvm-readelf \( -type f -o -type l \) 2>/dev/null | head -n1)"
    [ -n "$readelf" ] || readelf="$(command -v readelf || true)"
    if [ -n "$readelf" ]; then
        local align
        while read -r align; do
            if [ $((align)) -lt 16384 ]; then
                echo "ERROR: libLiteRtLmC.so has LOAD alignment $align < 0x4000 (16 KB pages)" >&2
                exit 1
            fi
        done < <("$readelf" -lW "$OUT_DIR/libLiteRtLmC.so" | awk '$1=="LOAD" { print $NF }')
        echo "OK: LOAD segments are 16 KB-aligned"
        echo "DT_NEEDED entries:"
        "$readelf" -d "$OUT_DIR/libLiteRtLmC.so" | grep NEEDED || true
        if "$readelf" -d "$OUT_DIR/libLiteRtLmC.so" | grep NEEDED | grep -vE 'lib(GemmaModelConstraintProvider|c|m|dl|log|android|c\+\+_shared|GLESv3|EGL)\.so' | grep . ; then
            echo "ERROR: unexpected DT_NEEDED entries above" >&2
            exit 1
        fi
    else
        echo "WARNING: no readelf found; skipping alignment/deps checks" >&2
    fi
}

build_ios() {
    [ "$(uname -s)" = "Darwin" ] || { echo "ios target requires a macOS host" >&2; exit 1; }
    echo "Building //litert_lm_dotnet:LiteRtLmC_xcframework with $BAZEL ..."
    ( cd "$SRC_DIR" && "$BAZEL" build //litert_lm_dotnet:LiteRtLmC_xcframework -c opt )

    local zip work
    zip="$(find "$BIN_DIR" -maxdepth 1 -name '*.xcframework.zip' | head -n1)"
    [ -n "$zip" ] || { echo "ERROR: no .xcframework.zip under $BIN_DIR" >&2; exit 1; }
    work="$(mktemp -d)"
    trap 'rm -rf "$work"' RETURN
    unzip -q "$zip" -d "$work"
    [ -d "$work/LiteRtLmC.xcframework" ] || { echo "ERROR: LiteRtLmC.xcframework not at zip root of $zip" >&2; exit 1; }

    # The Gemma provider ships as its own framework (a loose dylib can't be embedded
    # on iOS), so repoint the engine's load command at the framework binary.
    local slice
    for slice in "$work/LiteRtLmC.xcframework"/*/LiteRtLmC.framework/LiteRtLmC; do
        chmod +w "$slice"
        install_name_tool -change \
            "@rpath/libGemmaModelConstraintProvider.dylib" \
            "@rpath/GemmaModelConstraintProvider.framework/GemmaModelConstraintProvider" \
            "$slice"
        codesign -f -s - "$slice" 2>/dev/null || true

        # Sanity: C API exported, provider repointed, no dylib dep on LiteRt core.
        # (grep -c, not -q: -q's early exit SIGPIPEs nm, which pipefail reports as failure.)
        [ "$(nm -gU "$slice" | grep -c ' _litert_lm_')" -gt 0 ] \
            || { echo "ERROR: $slice exports no litert_lm_* symbols" >&2; exit 1; }
        if otool -L "$slice" | grep -qE 'libLiteRt\.dylib|libGemmaModelConstraintProvider\.dylib'; then
            echo "ERROR: $slice still references a bare dylib:" >&2
            otool -L "$slice" >&2
            exit 1
        fi
    done

    rm -f "$OUT_DIR/LiteRtLmC.xcframework.zip"
    ( cd "$work" && /usr/bin/zip -qry "$OUT_DIR/LiteRtLmC.xcframework.zip" "LiteRtLmC.xcframework" )

    # Wrap the prebuilt provider dylibs (device + simulator) into their own xcframework.
    FRAMEWORK_NAME="GemmaModelConstraintProvider" \
    SRC_DYLIB_NAME="libGemmaModelConstraintProvider.dylib" \
    BUNDLE_ID="com.koki-ibukuro.litertlm.GemmaModelConstraintProvider" \
    MIN_OS="15.0" \
        "$SCRIPT_DIR/../make-ios-xcframework.sh" "$SRC_DIR/prebuilt" "$OUT_DIR"
}

case "$TARGET" in
    host)                          build_host ;;
    android_arm64|android_x86_64)  build_android ;;
    ios)                           build_ios ;;
    *)
        echo "Unknown target: $TARGET (host|android_arm64|android_x86_64|ios)" >&2
        exit 1
        ;;
esac

echo "Output written to $OUT_DIR:"
ls -la "$OUT_DIR"
