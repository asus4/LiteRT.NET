#!/usr/bin/env bash
# Copies the canonical C# bindings (src/<project>) into the Unity packages; re-run after editing, CI diffs for drift.
# Source, not the prebuilt DLL: the iOS P/Invoke target must switch to "__Internal", which a DLL bakes in.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# src-project | unity-bindings-destination
PAIRS=(
    "src/LiteRT|unity/LiteRT/Runtime/Bindings"
    "src/LiteRT.LM|unity/LiteRT.LM/Runtime/Bindings"
)

for pair in "${PAIRS[@]}"; do
    SRC="$REPO_ROOT/${pair%%|*}"
    DST="$REPO_ROOT/${pair##*|}"

    mkdir -p "$DST/Interop"

    # Keep the asmdef and .meta files; replace only the synced sources.
    find "$DST" -name "*.cs" -delete

    cp "$SRC"/*.cs "$DST/"
    cp "$SRC"/Interop/*.cs "$DST/Interop/"

    echo "Synced ${pair%%|*} -> $DST"
    ( cd "$DST" && find . -name "*.cs" | sort )
done
