#!/usr/bin/env bash
# Copies the canonical C# bindings (src/LiteRT) into the Unity package so Unity compiles them
# from source. Source (not the prebuilt DLL) is required because the iOS P/Invoke target must be
# "__Internal" (see the `#if ... UNITY_IOS` in LiteRtNative.cs) — a precompiled DLL bakes "LiteRt".
# The committed copy keeps the UPM package self-contained; re-run after editing, CI diffs for drift.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$REPO_ROOT/src/LiteRT"
DST="$REPO_ROOT/unity/LiteRT/Runtime/Bindings"

mkdir -p "$DST/Interop"

# Keep the asmdef and .meta files; replace only the synced sources.
find "$DST" -name "*.cs" -delete

cp "$SRC"/*.cs "$DST/"
cp "$SRC"/Interop/*.cs "$DST/Interop/"

echo "Synced LiteRT bindings -> $DST"
( cd "$DST" && find . -name "*.cs" | sort )
