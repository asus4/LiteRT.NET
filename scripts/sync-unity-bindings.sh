#!/usr/bin/env bash
# Copies the canonical LiteRT C# bindings (src/LiteRT) into the LiteRT.Unity package so Unity
# compiles them from source.
#
# Why source (not the prebuilt NuGet DLL) in Unity: the iOS P/Invoke target must be
# "__Internal" (see the `#if ... UNITY_IOS` in LiteRtNative.cs). A precompiled netstandard2.1
# DLL bakes "LiteRt" and Unity uses it unchanged on iOS, so the conditional only takes effect
# when Unity itself compiles the source. The canonical source stays in src/LiteRT; this copy
# is generated (and committed so the UPM package is self-contained). Re-run after editing the
# bindings; CI can diff to catch drift.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$REPO_ROOT/src/LiteRT"
DST="$REPO_ROOT/src/LiteRT.Unity/Runtime/Bindings"

mkdir -p "$DST/Interop"

# Clear previously-synced sources (keep the asmdef and Unity .meta files).
find "$DST" -name "*.cs" -delete

cp "$SRC"/*.cs "$DST/"
cp "$SRC"/Interop/*.cs "$DST/Interop/"

echo "Synced LiteRT bindings -> $DST"
( cd "$DST" && find . -name "*.cs" | sort )
