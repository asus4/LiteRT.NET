# LiteRT.NET

C# / .NET and Unity bindings for [LiteRT](https://github.com/google-ai-edge/LiteRT) and [LiteRT-LM](https://github.com/google-ai-edge/LiteRT-LM).

## Packages

| Package | Contents |
| --- | --- |
| `LiteRT.Managed` | Managed bindings for the LiteRT core C API (`litert/c/*.h`). Hand-written P/Invoke. Depends on `LiteRT.Native`. |
| `LiteRT.Native` | Native LiteRT core runtime (`libLiteRt`) for all RIDs. CPU only. |
| `LiteRT.Gpu.Metal.Native` | Optional Metal GPU accelerator (Apple: macOS / iOS). Add alongside `LiteRT.Native`. For LiteRT-LM decode prefer `LiteRT.Gpu.WebGpu.Native` (Metal mis-computes LM logits). |
| `LiteRT.Gpu.WebGpu.Native` | Optional WebGPU (Dawn) GPU accelerator (desktop + Android). Required for LiteRT-LM GPU decode. |
| `LiteRT.Gpu.OpenCl.Native` | Optional OpenCL / GL GPU accelerator (Android). |
| `LiteRT.LM.Managed` | Managed bindings for the LiteRT-LM C API (`c/engine.h`). Depends on `LiteRT.LM.Native` and `LiteRT.Managed`. |
| `LiteRT.LM.Native` | Native LiteRT-LM runtime (`libLiteRtLmC` + Gemma constraint plugin) for all RIDs. |
| `LiteRT.Unity` (UPM) | Unity-only glue. **Not** a NuGet package — a UPM package at `src/LiteRT.Unity` (`com.github.asus4.litert`). Resolves the native plugin directory and feeds it to `LiteRT.LiteRtRuntime.NativeLibraryDirectory` so the runtime can `dlopen` accelerator plugins by absolute path under Unity. Used alongside the `LiteRT.Managed` / `LiteRT.Native` NuGet packages installed via NuGetForUnity. |

Managed packages target `netstandard2.1` (Unity / IL2CPP) and `net8.0`; their assembly
names stay `LiteRT` / `LiteRT.LM`. Native packages carry per-RID binaries under
`runtimes/<rid>/native` that resolve through the default .NET native-library search
(deps.json `NATIVE_DLL_SEARCH_DIRECTORIES`) — no environment variable or custom resolver.

GPU support is optional (the base install is CPU only), and each backend ships in its own
package so a consumer references exactly one. The core registry's probe order is hardcoded
in the prebuilt library and registers the first accelerator dylib on the load path, so the
active backend is simply whichever GPU package you reference.

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
`[DllImport]` resolves at runtime.

## Examples

Runnable samples live under `examples/` (`MinimalInference`, `SimpleLlm`, and the Unity
sample `MinimalInferenceUnity`). See **`examples/README.md`** for how to run them, the GPU
accelerator notes, and the Unity setup (NuGetForUnity, the Apple-Silicon plugin fix, and
how natives + model loading work under Unity).

## Repository layout

```
src/LiteRT/             Core managed bindings (LiteRT.Managed)
src/LiteRT.LM/          LiteRT-LM managed bindings (LiteRT.LM.Managed)
src/LiteRT.Native/      Core native runtime package (runtimes/<rid>/native)
src/LiteRT.Gpu.Metal.Native/   Optional Metal GPU accelerator package (Apple)
src/LiteRT.Gpu.WebGpu.Native/  Optional WebGPU accelerator package (desktop + Android)
src/LiteRT.Gpu.OpenCl.Native/  Optional OpenCL/GL accelerator package (Android)
src/LiteRT.LM.Native/   LM native runtime package
src/LiteRT.Unity/       Unity UPM package (com.github.asus4.litert): native-dir resolution glue
build/                  Shared MSBuild logic for the Native packages
examples/               Runnable samples (see examples/README.md)
examples/MinimalInferenceUnity/  Unity 6 sample consuming the NuGet packages via NuGetForUnity
native/fetch-natives.sh Populates src/*.Native/runtimes from prebuilt/SDK/out
native/litert-lm-c/     Bazel wrapper + build script for libLiteRtLmC
.github/workflows/      CI (managed) and native build matrix
```
