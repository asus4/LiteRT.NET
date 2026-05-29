# LiteRT.NET examples

Runnable samples for the LiteRT.NET bindings. See the root `AGENTS.md` for the package
layout, native-library story, and build details.

First populate the host RID's natives once (see root `AGENTS.md` for `fetch-natives.sh`):

```bash
scripts/fetch-natives.sh
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
# Build the LM native library once (Bazel; see scripts/litert-lm-c/build.sh):
scripts/litert-lm-c/build.sh /path/to/LiteRT-LM ./out
# Then fetch-natives.sh copies it (and the Gemma plugin) into LiteRT.LM.Native:
scripts/fetch-natives.sh

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

1. Populate natives + build the iOS xcframework, then pack: `scripts/fetch-natives.sh`
   (runs `scripts/make-ios-xcframework.sh` → `src/LiteRT.Native/runtimes/ios/native/LiteRt.xcframework.zip`),
   then `dotnet pack -c Release -o artifacts src/LiteRT.Native/LiteRT.Native.csproj`.
2. `Assets/NuGet.config` adds a local source: `<add key="litert-local" value="../../../artifacts" />`
   (path is relative to the `Assets/` dir holding NuGet.config). NuGetForUnity caches
   `NuGet.config` at Editor startup, so after editing it force a domain reload (recompile)
   before restoring, or it won't see the new source.
3. Install **`LiteRT.Native`** (the per-platform native runtimes). The managed **bindings are
   NOT consumed from NuGet in Unity** — they're compiled from source in `LiteRT.Unity` (see
   below), so `LiteRT.Managed` is intentionally absent from `packages.config`.

   Local-dev gotcha: NuGetForUnity caches packages at `~/.local/share/NuGet/Cache/`. If you
   re-`pack` the **same version** in place, clear that cached `.nupkg` (and delete
   `Assets/Packages/LiteRT.Native.*`) before restoring, or it reinstalls the stale copy
   (e.g. missing the iOS zip). Fresh consumers of a published version aren't affected.

### Apple Silicon plugin gotcha

NuGetForUnity's default `NativeRuntimeSettings` marks `osx-x64` as the Editor variant and
leaves `osx-arm64` runtime-only — backwards for an Apple Silicon Editor, so
`libLiteRt.dylib` won't load. Fix is committed at
`ProjectSettings/Packages/com.github-glitchenzo.nugetforunity/NativeRuntimeSettings.json`:
`osx-arm64` set to Editor `OS=OSX, CPU=ARM64`, `osx-x64` left build-only. The resulting
`.dylib.meta` shows `Editor: enabled=1, CPU=ARM64, OS=OSX`.

### Bindings are compiled from source in Unity

The `LiteRT.Unity` UPM package (`Packages/manifest.json` →
`"com.github.asus4.litert": "file:../../../src/LiteRT.Unity"`) ships the LiteRT C# bindings
as **source** (`src/LiteRT.Unity/Runtime/Bindings/`, synced from `src/LiteRT` by
`scripts/sync-unity-bindings.sh`), compiled into the **`LiteRT.Managed`** assembly (the asmdef
is named `LiteRT.Managed`, not `LiteRT`, to avoid colliding with the Windows native
`LiteRt.dll`). Source — not the prebuilt NuGet DLL — is required so the iOS
`#if __IOS__ || (UNITY_IOS && !UNITY_EDITOR)` in `LiteRtNative.cs` resolves the P/Invoke
target to `"__Internal"` (a precompiled DLL bakes `"LiteRt"` and can't switch). The
conditional is inert for `dotnet build`, so the NuGet packages are unaffected.

Under Unity, `[DllImport("LiteRt")]` (non-iOS) resolves through Unity's plugin system (the
imported `.dylib`/`.so`), **not** the deps.json `runtimes/<rid>/native` search the .NET CLI
uses. `LiteRt.Unity`'s `LiteRtNativeLibrary` runs at `[RuntimeInitializeOnLoadMethod]`, finds
`Assets/Packages/LiteRT.Native.*/runtimes/<rid>/native`, and sets the public
`LiteRtRuntime.NativeLibraryDirectory` hook (consulted first by
`NativeRuntime.ResolveLibraryDirectory()`) so the runtime can `dlopen` accelerator plugins by
absolute path. Harmless for CPU; required for GPU.

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
  The iOS `#if` in `LiteRtNative.cs` is not taken, so these use `[DllImport("LiteRt")]`.
- **iOS** — the prebuilt is a *dynamic* `libLiteRt.dylib`; iOS can't consume a loose
  `.dylib` (a device build needs a code-signed, embedded framework — otherwise Xcode fails
  with `library 'LiteRt' not found`). Two pieces work together:
  - **Delivery**: `scripts/make-ios-xcframework.sh` repackages the device + simulator dylibs
    into `LiteRt.xcframework` (binary renamed `LiteRt`, install name
    `@rpath/LiteRt.framework/LiteRt`), zips it, and `LiteRT.Native` ships it at
    `runtimes/ios/native/LiteRt.xcframework.zip` (matching `Microsoft.ML.OnnxRuntime`).
    `ios` is in the project's `NativeRuntimeSettings.json`, so NuGetForUnity extracts the
    zip as a plain asset (a `.zip` isn't a `PluginImporter`, so NuGetForUnity leaves it
    alone — no `-lLiteRt` auto-link error). `LiteRtPostprocessBuild` (an
    `IPostprocessBuildWithReport`) finds the zip under `Assets/Packages`, unzips it into the
    Xcode project, links it into `UnityFramework`, and embeds + code-signs it in the main app
    target.
  - **Symbol resolution**: with the framework linked (loaded at launch), the iOS
    `[DllImport("__Internal")]` (from the source-compiled bindings) resolves the symbols via
    `dlsym(RTLD_DEFAULT)`. (A named `dlopen("LiteRt")` does *not* find an embedded framework
    on iOS — verified — which is why `__Internal` is required.)
  - **Re-export after changes** (a stale Xcode export won't have the framework), then build
    in Xcode on device. Expect the same `[1.5811, 3.5355]` output and no `DllNotFoundException`.
