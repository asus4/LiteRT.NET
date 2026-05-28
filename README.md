# LiteRT.NET

🚧 Work in Progress 🚧

C# / .NET bindings for Google [LiteRT](https://github.com/google-ai-edge/LiteRT)
(on-device inference, formerly TensorFlow Lite) and
[LiteRT-LM](https://github.com/google-ai-edge/LiteRT-LM) (on-device LLM inference).
Unity is the primary target, but the core is a plain .NET / NuGet library; Unity-specific
pieces live in a separate repository.

## Packages

| Package | Contents |
| --- | --- |
| `LiteRT` | Managed bindings for the LiteRT core C API (`litert/c/*.h`). Hand-written P/Invoke. |
| `LiteRT.LM` | Managed bindings for the LiteRT-LM C API (`c/engine.h`). |

Both target `netstandard2.1` (Unity / IL2CPP) and `net8.0`. Native binaries ship in
per-RID `*.runtime.<rid>` packages; GPU accelerators are split into optional add-on packages.

## Native libraries

- **LiteRT core (`libLiteRt`)** — used by `LiteRT`. Prebuilt and shipped by Google
  (official C/C++ SDK, `storage.googleapis.com/litert/binaries/<ver>/`). For local
  development the prebuilt binaries under `LiteRT-LM/prebuilt/<platform>/` work too.
- **LiteRT-LM C API (`libLiteRtLmC`)** — used by `LiteRT.LM`. **Not** prebuilt upstream;
  we build it ourselves from `//c:engine` (see `native/litert-lm-c/`). CI
  (`.github/workflows/build-natives.yml`) builds it per platform and uploads it as a
  workflow artifact.

At runtime, `NativeLibraryResolver` maps the logical names (`LiteRt`, `LiteRtLmC`) to the
platform file and probes `runtimes/<rid>/native`. Set the `LITERT_NATIVE_DIR` environment
variable to point at a directory of native libraries during development.

## Examples

### MinimalInference (LiteRT core — works with prebuilt binaries)

```bash
LITERT_NATIVE_DIR=/path/to/LiteRT-LM/prebuilt/macos_arm64 \
  dotnet run --project examples/MinimalInference
```

Loads a small `.tflite` model, runs CPU inference, and prints the output tensor.

### SimpleLlm (LiteRT-LM — requires libLiteRtLmC)

```bash
# Build the native library once (Bazel; see native/litert-lm-c/build.sh):
native/litert-lm-c/build.sh /path/to/LiteRT-LM ./out

LITERT_NATIVE_DIR=$PWD/out \
  dotnet run --project examples/SimpleLlm -- /path/to/model.litertlm "Hello" cpu
```

## Repository layout

```
src/LiteRT/          Core managed bindings + native resolver
src/LiteRT.LM/       LiteRT-LM managed bindings
examples/            Runnable dotnet samples
native/litert-lm-c/  Bazel wrapper + build script for libLiteRtLmC
.github/workflows/   CI (managed) and native build matrix
```
