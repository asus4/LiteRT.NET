# LiteRT.NET examples

Runnable samples for the LiteRT.NET bindings. See the root `AGENTS.md` for the package
layout, native-library story, and build details.

First populate the host RID's natives once (see root `AGENTS.md` for `fetch-natives.sh`):

```bash
LITERT_RIDS=osx-arm64 native/fetch-natives.sh
```

## MinimalInference (LiteRT core)

```bash
dotnet run --project examples/MinimalInference                       # CPU (default)
dotnet run --project examples/MinimalInference -- <model.tflite> gpu  # GPU
```

Loads a small `.tflite` model, runs inference, and prints the output tensor. The second
argument selects the accelerator (`cpu` or `gpu`). GPU requires a backend package; this
example references `LiteRT.Gpu.Metal.Native` (Metal — the better fit for Unity on Apple)
so the accelerator dylib sits beside the core library. `LiteRtEnvironment` sets the
`RuntimeLibraryDir` option so the runtime can load it, and limits auto-registration to the
requested accelerators (a CPU-only run stays quiet instead of warning about absent
GPU/NPU plugins).

## SimpleLlm (LiteRT-LM — requires libLiteRtLmC)

```bash
# Build the LM native library once (Bazel; see native/litert-lm-c/build.sh):
native/litert-lm-c/build.sh /path/to/LiteRT-LM ./out
# Then fetch-natives.sh copies it (and the Gemma plugin) into LiteRT.LM.Native:
LITERT_RIDS=osx-arm64 native/fetch-natives.sh

dotnet run --project examples/SimpleLlm -- /path/to/model.litertlm "Hello" cpu
dotnet run --project examples/SimpleLlm -- /path/to/model.litertlm "Hello" gpu
```

For GPU decode this example references `LiteRT.Gpu.WebGpu.Native`: the Metal accelerator
mis-computes LM logits (produces a repetition loop), whereas WebGPU/Dawn is correct.
On-GPU sampling falls back to CPU sampling (the prebuilt GPU samplers are not shipped),
which yields correct output.

## MinimalInferenceUnity (LiteRT core, in Unity)

Unity 6 (`6000.3.11f1`) port of MinimalInference at `examples/MinimalInferenceUnity`.
Consumes the NuGet packages from the repo's `artifacts/` folder via **NuGetForUnity**
(`com.github-glitchenzo.nugetforunity`), plus the `LiteRT.Unity` UPM package (`src/LiteRT.Unity`).
Verified end-to-end on the macOS Editor (Apple Silicon, CPU).

### Setup

1. `dotnet pack -c Release -o artifacts src/LiteRT/LiteRT.csproj` (+ `LiteRT.Native`) so the
   `.nupkg`s exist in `artifacts/`.
2. `Assets/NuGet.config` adds a local source: `<add key="litert-local" value="../../../artifacts" />`
   (path is relative to the `Assets/` dir holding NuGet.config). NuGetForUnity caches
   `NuGet.config` at Editor startup, so after editing it force a domain reload (recompile)
   before restoring, or it won't see the new source.
3. Install `LiteRT.Managed` (pulls `LiteRT.Native`) → extracted to `Assets/Packages/`.

### Apple Silicon plugin gotcha

NuGetForUnity's default `NativeRuntimeSettings` marks `osx-x64` as the Editor variant and
leaves `osx-arm64` runtime-only — backwards for an Apple Silicon Editor, so
`libLiteRt.dylib` won't load. Fix is committed at
`ProjectSettings/Packages/com.github-glitchenzo.nugetforunity/NativeRuntimeSettings.json`:
`osx-arm64` set to Editor `OS=OSX, CPU=ARM64`, `osx-x64` left build-only. The resulting
`.dylib.meta` shows `Editor: enabled=1, CPU=ARM64, OS=OSX`.

### How natives + model loading work under Unity

The `LiteRT.Unity` UPM package is referenced via `Packages/manifest.json`
(`"com.github.asus4.litert": "file:../../../src/LiteRT.Unity"`). Its `LiteRtNativeLibrary`
runs at `[RuntimeInitializeOnLoadMethod]`, finds
`Assets/Packages/LiteRT.Native.*/runtimes/<rid>/native`, and sets
`LiteRtRuntime.NativeLibraryDirectory`. Harmless for CPU; required for GPU/IL2CPP.

Under Unity, `[DllImport("LiteRt")]` resolves through Unity's plugin system (the imported
`.dylib`/`.so`), **not** the deps.json `runtimes/<rid>/native` search the .NET CLI uses.
`LiteRtRuntime.NativeLibraryDirectory` (public hook in `src/LiteRT/NativeRuntime.cs`,
consulted first by `ResolveLibraryDirectory()`) bridges the gap for the `RuntimeLibraryDir`
accelerator-plugin path.

The sample MonoBehaviour (`Assets/Scripts/MinimalInferenceBehaviour.cs`) reads the model
from `StreamingAssets` with `UnityWebRequest` (modern `async Awaitable` + `await
SendWebRequest()`). This is cross-platform: Android's `StreamingAssets` lives inside the
APK at a `jar:file://…!/assets/…` URL — not a real path, so `File.Exists`/a file path fail
there. It then loads via `LiteRtModel.CreateFromBuffer(byte[])`, which pins the buffer for
the model's lifetime (the C header requires the buffer stay valid; the runtime does not
copy) and frees it on `Dispose`. Use `CreateFromBuffer` rather than `CreateFromFile`
whenever no real file path exists.

### Platform notes

- **macOS Editor / Android** — work directly off the NuGetForUnity-imported natives
  (`libLiteRt.dylib` / `libLiteRt.so`); Android loads the loose `.so` via the OS loader.
- **iOS** — needs special handling: the iOS prebuilt is a *dynamic* `libLiteRt.dylib`, and
  iOS can't consume a loose `.dylib` (a device build needs a code-signed, embedded
  framework, otherwise Xcode fails with `library 'LiteRt' not found`). So:
  - `native/make-ios-xcframework.sh` repackages the device + simulator dylibs into
    `LiteRt.xcframework` (binary renamed `LiteRt`, install name
    `@rpath/LiteRt.framework/LiteRt`) under `src/LiteRT.Unity/Plugins/iOS~/`. The `~`
    suffix keeps Unity from importing it as a loose plugin (which is what caused the
    `-lLiteRt` link error).
  - The bare iOS dylib is excluded from the Unity project: the `ios`/`ios-arm64` entries
    were removed from `NativeRuntimeSettings.json` so NuGetForUnity no longer extracts it.
  - `LiteRT.Unity`'s Editor post-build hook (`LiteRtIosBuildProcessor`, an
    `IPostprocessBuildWithReport`) copies the xcframework into the generated Xcode project,
    links it into the `UnityFramework` target, and embeds + code-signs it in the main app
    target. At runtime dyld already has the framework loaded, so the unchanged
    `[DllImport("LiteRt")]` resolves it by leaf name — **no `__Internal`, no change to the
    NuGet bindings**.
  - After pulling these changes, **re-export the iOS project** (a stale export won't have
    the xcframework), then rebuild in Xcode. Expect the same `[1.5811, 3.5355]` output.
