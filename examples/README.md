# LiteRT.NET examples

Populate the native libraries first — see the root [README.md](../README.md) for
`fetch-natives.sh` (and, for SimpleChat, `scripts/litert-lm-c/build.sh`).

## MinimalInference (LiteRT core)

```bash
# CPU
dotnet run --project examples/MinimalInference
# GPU
dotnet run --project examples/MinimalInference -- <model.tflite> gpu
```

GPU references `LiteRT.Gpu.Metal`; `LiteRtEnvironment` sets `RuntimeLibraryDir` and
limits auto-registration to the requested accelerators.

## SimpleChat (LiteRT-LM — requires libLiteRtLmC)

```bash
dotnet run --project examples/SimpleChat -- /path/to/model.litertlm "Hello" cpu
dotnet run --project examples/SimpleChat -- /path/to/model.litertlm "Hello" gpu
```

- GPU references `LiteRT.Gpu.WebGpu`, not Metal — the Metal accelerator mis-computes LM
  logits (repetition loop).
- On-GPU TopK sampling falls back to CPU sampling on macOS (no working GPU sampler; see
  `classify()` in `scripts/fetch-natives.sh`).

## MinimalInferenceUnity (LiteRT core, in Unity)

Unity 6 (`6000.3.11f1`) port at `examples/MinimalInferenceUnity`. Consumes
`com.koki-ibukuro.litert` (`unity/LiteRT`) and `com.koki-ibukuro.litert.unity`
(`unity/LiteRT.Unity`) by local `file:` reference — no NuGetForUnity. Verified on the
macOS Editor (Apple Silicon, CPU).

### Setup

Binaries under `unity/LiteRT/Plugins/` are `.gitignore`'d (only `.meta` files are
committed). Before opening the project:

1. `scripts/fetch-natives.sh` — core natives into `src/LiteRT/runtimes/<rid>/native`
   (on macOS also builds `LiteRt.xcframework.zip` via `scripts/make-ios-xcframework.sh`).
2. `scripts/sync-unity-natives.sh` — copies them into `unity/LiteRT/Plugins/<platform>`.
3. `scripts/sync-unity-bindings.sh` — copies C# bindings into
   `unity/LiteRT/Runtime/Bindings` (re-run after editing bindings; CI diffs for drift).

The committed `.meta` files set the correct platform/CPU per binary, including the
Apple-Silicon Editor variant.

### Notes

- Bindings compile from **source** in Unity (asmdef `LiteRT.Managed` — named to avoid
  colliding with the Windows native `LiteRt.dll`) because the iOS `#if` in
  `LiteRtNative.cs` must resolve the P/Invoke target to `"__Internal"`; a prebuilt DLL
  bakes `"LiteRt"`.
- `LiteRtNativeLibrary` sets `LiteRtRuntime.NativeLibraryDirectory` to the package's
  `Plugins/<platform>` dir at load, so accelerator plugins can be `dlopen`'d by absolute
  path (required for GPU).
- Load models from `StreamingAssets` with `UnityWebRequest` +
  `LiteRtModel.CreateFromBuffer` (`LiteRtModelLoader` does this) — Android's
  StreamingAssets is not a real file path, and the buffer must stay valid until
  `Dispose`.
- iOS: `scripts/make-ios-xcframework.sh` repackages the device + simulator dylibs into
  `LiteRt.xcframework`, which `sync-unity-natives.sh` places in `unity/LiteRT/Plugins/iOS`
  and Unity embeds — iOS can't load a loose dylib, and `[DllImport("__Internal")]`
  resolves the embedded framework's symbols. Re-export the Xcode project after binding
  changes.
