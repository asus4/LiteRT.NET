# LiteRT.NET

C# / .NET and Unity bindings for [LiteRT](https://github.com/google-ai-edge/LiteRT) and
[LiteRT-LM](https://github.com/google-ai-edge/LiteRT-LM). Managed packages (`LiteRT.Managed`,
`LiteRT.LM.Managed`) are hand-written P/Invoke targeting `netstandard2.1` + `net8.0`; native
runtimes ship per-RID under `runtimes/<rid>/native`. GPU backends are optional, one package
each (Metal / WebGpu / OpenCl). Unity consumes the `unity/` UPM packages.

## Reproduce

Native binaries are generated, not committed — populate them before building:

```sh
scripts/fetch-natives.sh                          # all RIDs (best-effort)
LITERT_RIDS=osx-arm64 scripts/fetch-natives.sh    # just the host RID
```

`libLiteRtLmC` (the LiteRT-LM C API) isn't prebuilt upstream; build it once for the host
before the SimpleLlm example, then re-run `fetch-natives.sh` to collect it:

```sh
scripts/litert-lm-c/build.sh /path/to/LiteRT-LM ./out
```

For Unity, also sync the bindings + natives into the UPM package:

```sh
scripts/sync-unity-bindings.sh                    # src/LiteRT -> unity/LiteRT/Runtime/Bindings
scripts/sync-unity-natives.sh                     # runtimes  -> unity/LiteRT/Plugins
```

## Test

Build the managed packages and run the examples as smoke tests:

```sh
dotnet build -c Release src/LiteRT/LiteRT.csproj
dotnet build -c Release src/LiteRT.LM/LiteRT.LM.csproj

dotnet run --project examples/MinimalInference                         # core; prints output tensor
dotnet run --project examples/SimpleLlm -- /path/to/model.litertlm "Hello" cpu
```

Unity: open `examples/MinimalInferenceUnity` (Unity 6) after the sync scripts above. See
`examples/README.md` for example details and GPU notes, and
`docs/gpu-accelerator-probe-order.md` for how the active GPU backend is selected.

## Layout

```
src/LiteRT/             Core managed bindings (LiteRT.Managed)
src/LiteRT.LM/          LiteRT-LM managed bindings (LiteRT.LM.Managed)
src/LiteRT.Native/      Core native runtime package (runtimes/<rid>/native)
src/LiteRT.Gpu.*.Native/  Optional GPU accelerator packages (Metal / WebGpu / OpenCl)
src/LiteRT.LM.Native/   LM native runtime package
unity/LiteRT/           Unity core UPM package: bindings (Runtime/Bindings) + natives (Plugins)
unity/LiteRT.Unity/     Unity utilities UPM package (LiteRtModelLoader)
build/                  Shared MSBuild logic for the Native packages
examples/               Runnable samples (MinimalInference, SimpleLlm, MinimalInferenceUnity)
scripts/                Build/dev automation (fetch-natives, sync-unity-*, make-ios-xcframework)
.github/workflows/      CI (managed) and native build matrix
```
