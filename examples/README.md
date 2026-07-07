# LiteRT.NET examples

Runnable samples for the LiteRT.NET bindings. See the root `AGENTS.md` for the package
layout, native-library story, and build details.

First populate the host RID's natives once (see root `AGENTS.md` for `fetch-natives.sh`):

```bash
scripts/fetch-natives.sh
```

## MinimalInference (LiteRT core)

```bash
# CPU
dotnet run --project examples/MinimalInference
# GPU                       
dotnet run --project examples/MinimalInference -- <model.tflite> gpu
```

Loads a small `.tflite` model, runs inference, and prints the output tensor. The second
argument selects the accelerator (`cpu` or `gpu`). GPU requires a backend package; this
example references `LiteRT.Gpu.Metal` (Metal — the better fit for Unity on Apple)
so the accelerator dylib sits beside the core library. `LiteRtEnvironment` sets the
`RuntimeLibraryDir` option so the runtime can load it, and limits auto-registration to the
requested accelerators (a CPU-only run stays quiet instead of warning about absent
GPU/NPU plugins).

## SimpleLlm (LiteRT-LM — requires libLiteRtLmC)

```bash
# Initial setup:
scripts/litert-lm-c/build.sh /path/to/LiteRT-LM ./out
scripts/fetch-natives.sh

dotnet run --project examples/SimpleLlm -- /path/to/model.litertlm "Hello" cpu
dotnet run --project examples/SimpleLlm -- /path/to/model.litertlm "Hello" gpu
```

For GPU decode this example references `LiteRT.Gpu.WebGpu`: the Metal accelerator
mis-computes LM logits (repetition loop), whereas WebGPU/Dawn is correct. On-GPU TopK
sampling falls back to CPU sampling (correct output) because macOS ships no working GPU
sampler today — see `classify()` in `scripts/fetch-natives.sh` for why.

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
   `src/LiteRT/runtimes/<rid>/native` (on macOS it also builds the iOS
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
- **iOS** — the prebuilt is a *dynamic* `libLiteRt.dylib`, which iOS can't consume loose (a
  device build needs a code-signed, embedded framework). `scripts/make-ios-xcframework.sh`
  repackages the device + simulator dylibs into `LiteRt.xcframework.zip` (shipped at
  `unity/LiteRT/Plugins/iOS/`); Unity imports the `.zip` as a plain asset, and
  `LiteRtPostprocessBuild` unzips, links, and embeds + code-signs it into the Xcode project.
  With the framework loaded at launch, the iOS `[DllImport("__Internal")]` resolves symbols
  via `dlsym(RTLD_DEFAULT)` — a named `dlopen("LiteRt")` does *not* find an embedded framework
  on iOS, which is why `__Internal` is required. Re-export after binding changes (a stale Xcode
  export won't have the framework), then build on device: expect the same `[1.5811, 3.5355]`
  output and no `DllNotFoundException`.
