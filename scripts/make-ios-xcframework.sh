#!/usr/bin/env bash
# Wraps the prebuilt iOS core dylibs (device + simulator) into a dynamic LiteRt.xcframework,
# zipped, for the LiteRT package payload (runtimes/ios/native/).
#
# Why: a loose `.dylib` can't be code-signed/embedded on an iOS device build, so (matching
# Microsoft.ML.OnnxRuntime) we ship a zipped `.xcframework`. The binary is renamed `LiteRt`
# with install name `@rpath/LiteRt.framework/LiteRt`; iOS resolves symbols via
# `[DllImport("__Internal")]` against the linked+embedded framework (see LiteRtNative.cs).
# Consumed by Unity (sync-unity-natives.sh unzips into Plugins/iOS/<Name>.xcframework,
# imported as a plugin with AddToEmbeddedBinaries) and, eventually, .NET-for-iOS / MAUI
# (NativeReference).
#
# Usage: make-ios-xcframework.sh [PREBUILT_DIR] [OUT_DIR]
#   PREBUILT_DIR default ../LiteRT-LM/prebuilt (expects ios_arm64/, ios_sim_arm64/)
#   OUT_DIR      default src/LiteRT/runtimes/ios/native
# Env (defaults = the LiteRT core framework; override to wrap other prebuilt dylibs,
# e.g. build.sh ios wraps libGemmaModelConstraintProvider this way):
#   FRAMEWORK_NAME, SRC_DYLIB_NAME, BUNDLE_ID, MIN_OS
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PREBUILT_DIR="${1:-$REPO_ROOT/../LiteRT-LM/prebuilt}"
OUT_DIR="${2:-$REPO_ROOT/src/LiteRT/runtimes/ios/native}"

FRAMEWORK_NAME="${FRAMEWORK_NAME:-LiteRt}"
SRC_DYLIB_NAME="${SRC_DYLIB_NAME:-libLiteRt.dylib}"
BUNDLE_ID="${BUNDLE_ID:-com.koki-ibukuro.litert.LiteRt}"
MIN_OS="${MIN_OS:-14.0}"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# make_framework <framework-dir> <dylib> <platform: iPhoneOS|iPhoneSimulator>
make_framework() {
    local fw="$1" dylib="$2" platform="$3"
    [ -f "$dylib" ] || { echo "ERROR: missing $dylib" >&2; exit 1; }
    echo "Building $platform framework: $fw"
    mkdir -p "$fw"
    cp "$dylib" "$fw/$FRAMEWORK_NAME"
    chmod +w "$fw/$FRAMEWORK_NAME"
    install_name_tool -id "@rpath/$FRAMEWORK_NAME.framework/$FRAMEWORK_NAME" "$fw/$FRAMEWORK_NAME"
    cat > "$fw/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key><string>en</string>
    <key>CFBundleExecutable</key><string>$FRAMEWORK_NAME</string>
    <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>CFBundleName</key><string>$FRAMEWORK_NAME</string>
    <key>CFBundlePackageType</key><string>FMWK</string>
    <key>CFBundleShortVersionString</key><string>1.0</string>
    <key>CFBundleVersion</key><string>1</string>
    <key>MinimumOSVersion</key><string>$MIN_OS</string>
    <key>CFBundleSupportedPlatforms</key><array><string>$platform</string></array>
</dict>
</plist>
PLIST
}

DEVICE_FW="$WORK/device/$FRAMEWORK_NAME.framework"
SIM_FW="$WORK/sim/$FRAMEWORK_NAME.framework"
make_framework "$DEVICE_FW" "$PREBUILT_DIR/ios_arm64/$SRC_DYLIB_NAME"     "iPhoneOS"
make_framework "$SIM_FW"    "$PREBUILT_DIR/ios_sim_arm64/$SRC_DYLIB_NAME" "iPhoneSimulator"

echo "Creating xcframework..."
xcodebuild -create-xcframework \
    -framework "$DEVICE_FW" \
    -framework "$SIM_FW" \
    -output "$WORK/$FRAMEWORK_NAME.xcframework"

OUT_ZIP="$OUT_DIR/$FRAMEWORK_NAME.xcframework.zip"
mkdir -p "$OUT_DIR"
rm -f "$OUT_ZIP"

echo "Zipping -> $OUT_ZIP"
# .xcframework as the top-level entry (matches onnxruntime.xcframework.zip).
( cd "$WORK" && zip -qry "$OUT_ZIP" "$FRAMEWORK_NAME.xcframework" )

echo "Done: $OUT_ZIP"
