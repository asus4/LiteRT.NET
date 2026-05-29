#!/usr/bin/env bash
# Wraps the prebuilt iOS LiteRT core dylib (device + simulator) into a dynamic
# LiteRt.xcframework and zips it into the LiteRT.Native NuGet package payload.
#
# Why: the iOS prebuilt is a bare dynamic `libLiteRt.dylib`. A loose .dylib cannot be
# code-signed/embedded on an iOS device build. The standard fix (matching
# Microsoft.ML.OnnxRuntime) is to ship the iOS binary as an `.xcframework`, zipped, under
# `runtimes/ios/native/` in the NuGet package (NuGet/MSBuild can't carry a `.xcframework`
# *directory* cleanly). The binary is renamed `LiteRt` with install name
# `@rpath/LiteRt.framework/LiteRt`; symbols are resolved on iOS via `[DllImport("__Internal")]`
# against the linked+embedded framework (see LiteRtNative.cs).
#
# The xcframework.zip is consumed by:
#   - .NET-for-iOS / MAUI: a build/net*-ios targets file (NativeReference) — deferred.
#   - Unity: NuGetForUnity extracts the zip; LiteRtPostprocessBuild unzips + embeds it.
#
# Usage:
#   scripts/make-ios-xcframework.sh [PREBUILT_DIR] [OUT_DIR]
#   PREBUILT_DIR default: ../LiteRT-LM/prebuilt   (expects ios_arm64/ and ios_sim_arm64/)
#   OUT_DIR      default: src/LiteRT.Native/runtimes/ios/native
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PREBUILT_DIR="${1:-$REPO_ROOT/../LiteRT-LM/prebuilt}"
OUT_DIR="${2:-$REPO_ROOT/src/LiteRT.Native/runtimes/ios/native}"

FRAMEWORK_NAME="LiteRt"
BUNDLE_ID="com.github.asus4.litert.LiteRt"
MIN_OS="14.0"

DEVICE_DYLIB="$PREBUILT_DIR/ios_arm64/libLiteRt.dylib"
SIM_DYLIB="$PREBUILT_DIR/ios_sim_arm64/libLiteRt.dylib"

for f in "$DEVICE_DYLIB" "$SIM_DYLIB"; do
    [ -f "$f" ] || { echo "ERROR: missing $f" >&2; exit 1; }
done

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Builds a flat iOS .framework from a dylib at $1 into dir $2, with the given supported
# platform ($3 = iPhoneOS | iPhoneSimulator).
make_framework() {
    local dylib="$1" fwdir="$2" platform="$3"
    local fw="$fwdir/$FRAMEWORK_NAME.framework"
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

echo "Building device framework..."
make_framework "$DEVICE_DYLIB" "$WORK/device" "iPhoneOS"
echo "Building simulator framework..."
make_framework "$SIM_DYLIB" "$WORK/sim" "iPhoneSimulator"

WORK_XC="$WORK/$FRAMEWORK_NAME.xcframework"
echo "Creating xcframework..."
xcodebuild -create-xcframework \
    -framework "$WORK/device/$FRAMEWORK_NAME.framework" \
    -framework "$WORK/sim/$FRAMEWORK_NAME.framework" \
    -output "$WORK_XC"

OUT_ZIP="$OUT_DIR/$FRAMEWORK_NAME.xcframework.zip"
mkdir -p "$OUT_DIR"
rm -f "$OUT_ZIP"

echo "Zipping -> $OUT_ZIP"
# Zip with the .xcframework as the top-level entry (cd into WORK so the archive root is the
# bundle, matching Microsoft.ML.OnnxRuntime's onnxruntime.xcframework.zip layout).
( cd "$WORK" && zip -qry "$OUT_ZIP" "$FRAMEWORK_NAME.xcframework" )

echo "Done: $OUT_ZIP"
