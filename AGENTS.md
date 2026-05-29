# LiteRT.NET

C# / .NET and Unity bindings for [LiteRT](https://github.com/google-ai-edge/LiteRT) and [LiteRT-LM](https://github.com/google-ai-edge/LiteRT-LM).

## Packages

| Package | Contents |
| --- | --- |
| `LiteRT.Managed` | Managed bindings for the LiteRT core C API (`litert/c/*.h`). Hand-written P/Invoke. Depends on `LiteRT.Native`. |
| `LiteRT.Native` | Native LiteRT core runtime (`libLiteRt`) for all RIDs. CPU only. |
| `LiteRT.Gpu.Native` | Optional GPU accelerators (Metal / WebGpu / OpenCl) + samplers. Add alongside `LiteRT.Native`. |
| `LiteRT.LM.Managed` | Managed bindings for the LiteRT-LM C API (`c/engine.h`). Depends on `LiteRT.LM.Native` and `LiteRT.Managed`. |
| `LiteRT.LM.Native` | Native LiteRT-LM runtime (`libLiteRtLmC` + Gemma constraint plugin) for all RIDs. |

Managed packages target `netstandard2.1` (Unity / IL2CPP) and `net8.0`; their assembly
names stay `LiteRT` / `LiteRT.LM`. Native packages carry per-RID binaries under
`runtimes/<rid>/native` and resolve at runtime via the default .NET native-library
search (deps.json `NATIVE_DLL_SEARCH_DIRECTORIES`) — no environment variable or custom
resolver. GPU accelerators are an optional add-on; the base install is CPU only.

## Native libraries

- **LiteRT core (`libLiteRt`)** — used by `LiteRT.Managed`. Prebuilt and shipped by Google
  (official C/C++ SDK, `storage.googleapis.com/litert/binaries/<ver>/`). For local
  development the prebuilt binaries under `LiteRT-LM/prebuilt/<platform>/` work too.
- **LiteRT-LM C API (`libLiteRtLmC`)** — used by `LiteRT.LM.Managed`. **Not** prebuilt
  upstream; we build it ourselves from `//c:engine` (see `native/litert-lm-c/`). CI
  (`.github/workflows/build-natives.yml`) builds it per platform and uploads it as a
  workflow artifact.

Binaries are generated, not authored: CI fetches/builds them into `src/*.Native/runtimes`.
For local builds, run the fetch script to populate `src/*.Native/runtimes/<rid>/native`
from the local prebuilt SDK, the locally built LM library (`out/`), and the official SDK:

```bash
native/fetch-natives.sh            # all RIDs (best-effort)
LITERT_RIDS=osx-arm64 native/fetch-natives.sh   # just the host RID
```

The fetched binaries flow into the `dotnet` build output automatically (ProjectReference
copy-to-output for examples; `runtimes/<rid>/native` + deps.json for NuGet consumers), so
`[DllImport]` resolves with no environment variable.

## Examples

First populate the host RID's natives once:

```bash
LITERT_RIDS=osx-arm64 native/fetch-natives.sh
```

### MinimalInference (LiteRT core)

```bash
dotnet run --project examples/MinimalInference
```

Loads a small `.tflite` model, runs CPU inference, and prints the output tensor.

### SimpleLlm (LiteRT-LM — requires libLiteRtLmC)

```bash
# Build the LM native library once (Bazel; see native/litert-lm-c/build.sh):
native/litert-lm-c/build.sh /path/to/LiteRT-LM ./out
# Then fetch-natives.sh copies it (and the Gemma plugin) into LiteRT.LM.Native:
LITERT_RIDS=osx-arm64 native/fetch-natives.sh

dotnet run --project examples/SimpleLlm -- /path/to/model.litertlm "Hello" cpu
```

## Repository layout

```
src/LiteRT/             Core managed bindings (LiteRT.Managed)
src/LiteRT.LM/          LiteRT-LM managed bindings (LiteRT.LM.Managed)
src/LiteRT.Native/      Core native runtime package (runtimes/<rid>/native)
src/LiteRT.Gpu.Native/  Optional GPU accelerator package
src/LiteRT.LM.Native/   LM native runtime package
build/                  Shared MSBuild logic for the Native packages
examples/               Runnable dotnet samples
native/fetch-natives.sh Populates src/*.Native/runtimes from prebuilt/SDK/out
native/litert-lm-c/     Bazel wrapper + build script for libLiteRtLmC
.github/workflows/      CI (managed) and native build matrix
```
