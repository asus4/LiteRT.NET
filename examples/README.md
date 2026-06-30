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
On-GPU TopK sampling falls back to CPU sampling (which yields correct output): on macOS
the prebuilt GPU samplers are not shipped because neither works for LM today — the WebGPU
sampler dylib is stale (missing the `UpdateConfig`/`CanHandleInput`/… symbols current
`libLiteRtLmC` requires, so it fails to load), and the Metal sampler loads but only engages
under the logit-broken Metal accelerator. See `scripts/fetch-natives.sh` `classify()`.

## MinimalInferenceUnity (LiteRT core, in Unity)

Unity 6 (`6000.3.11f1`) port of MinimalInference at `examples/MinimalInferenceUnity`.
Consumes the two Unity UPM packages by local `file:` reference (`Packages/manifest.json`):
`com.github.asus4.litert` (`unity/LiteRT`) and `com.github.asus4.litert.unity`
(`unity/LiteRT.Unity`). **No NuGetForUnity** — the packages are self-contained.
Verified end-to-end on the macOS Editor (Apple Silicon, CPU).

### Setup

`unity/LiteRT` ships its native libraries under `Plugins/`, but the binaries are
`.gitignore`'d (only the `.meta` files are committed). Populate them before opening the
project:

1. `scripts/fetch-natives.sh` — fetches/builds the core natives into
   `src/LiteRT.Native/runtimes/<rid>/native` (on macOS it also builds the iOS
   `LiteRt.xcframework.zip` via `scripts/make-ios-xcframework.sh`).
2. `scripts/sync-unity-natives.sh` — copies those into `unity/LiteRT/Plugins/<platform>`.
3. `scripts/sync-unity-bindings.sh` — copies the C# bindings from `src/LiteRT` into
   `unity/LiteRT/Runtime/Bindings` (re-run after editing the bindings; CI diffs to catch drift).

Then open `examples/MinimalInferenceUnity` in Unity. The committed `.meta` files set the
correct platform/CPU per binary — including the Apple-Silicon Editor variant for macOS
(`Editor: enabled=1, CPU=ARM64, OS=OSX`) — so no manual plugin import-setting fixes are
needed.

### Bindings are compiled from source in Unity

`unity/LiteRT/Runtime/Bindings/` holds the LiteRT C# bindings as **source** (synced from
`src/LiteRT` by `scripts/sync-unity-bindings.sh`), compiled into the **`LiteRT.Managed`**
assembly (asmdef named `LiteRT.Managed`, not `LiteRT`, to avoid colliding with the Windows
native `LiteRt.dll`). Source — not the prebuilt NuGet DLL — is required so the iOS
`#if __IOS__ || (UNITY_IOS && !UNITY_EDITOR)` in `LiteRtNative.cs` resolves the P/Invoke
target to `"__Internal"` (a precompiled DLL bakes `"LiteRt"` and can't switch). The
conditional is inert for `dotnet build`, so the NuGet packages are unaffected.

Under Unity, `[DllImport("LiteRt")]` (non-iOS) resolves through Unity's plugin system (the
imported `.dylib`/`.so`/`.dll`), **not** the deps.json `runtimes/<rid>/native` search the
.NET CLI uses. `LiteRtNativeLibrary` (in `unity/LiteRT`) runs at
`[RuntimeInitializeOnLoadMethod]`, resolves this package's `Plugins/<platform>` directory via
`UnityEditor.PackageManager.PackageInfo`, and sets the public
`LiteRtRuntime.NativeLibraryDirectory` hook (consulted first by
`NativeRuntime.ResolveLibraryDirectory()`) so the runtime can `dlopen` accelerator plugins by
absolute path. Harmless for CPU; required for GPU.

### Model loading

`LiteRtModelLoader` (in `com.github.asus4.litert.unity`) handles loading. The sample
MonoBehaviour (`Assets/Scripts/MinimalInferenceBehaviour.cs`) reads the model from
`StreamingAssets` with `UnityWebRequest` (modern `async Awaitable` + `await
SendWebRequest()`). This is cross-platform: Android's `StreamingAssets` lives inside the APK
at a `jar:file://…!/assets/…` URL — not a real path, so `File.Exists`/a file path fail there.
The loader then uses `LiteRtModel.CreateFromBuffer`, which references the buffer for the
model's lifetime (the C header requires the buffer stay valid; the runtime does not copy) and
frees it on `Dispose`. Use `CreateFromBuffer` rather than `CreateFromFile` whenever no real
file path exists.

### Platform notes

- **macOS Editor / Android** — load directly off the `Plugins/` natives
  (`libLiteRt.dylib` / `libLiteRt.so`); the committed `.meta` enables the right platform/CPU.
  The iOS `#if` in `LiteRtNative.cs` is not taken, so these use `[DllImport("LiteRt")]`.
- **iOS** — the prebuilt is a *dynamic* `libLiteRt.dylib`; iOS can't consume a loose
  `.dylib` (a device build needs a code-signed, embedded framework — otherwise Xcode fails
  with `library 'LiteRt' not found`). Two pieces work together:
  - **Delivery**: `scripts/make-ios-xcframework.sh` repackages the device + simulator dylibs
    into `LiteRt.xcframework` (binary renamed `LiteRt`, install name
    `@rpath/LiteRt.framework/LiteRt`), zips it, and it ships at
    `unity/LiteRT/Plugins/iOS/LiteRt.xcframework.zip`. A `.zip` isn't a `PluginImporter`, so
    Unity imports it as a plain asset (no `-lLiteRt` auto-link error). `LiteRtPostprocessBuild`
    (an `IPostprocessBuildWithReport`) finds the zip in the package, unzips it into the Xcode
    project, links it into `UnityFramework`, and embeds + code-signs it in the main app target.
  - **Symbol resolution**: with the framework linked (loaded at launch), the iOS
    `[DllImport("__Internal")]` (from the source-compiled bindings) resolves the symbols via
    `dlsym(RTLD_DEFAULT)`. (A named `dlopen("LiteRt")` does *not* find an embedded framework
    on iOS — verified — which is why `__Internal` is required.)
  - **Re-export after changes** (a stale Xcode export won't have the framework), then build
    in Xcode on device. Expect the same `[1.5811, 3.5355]` output and no `DllNotFoundException`.
