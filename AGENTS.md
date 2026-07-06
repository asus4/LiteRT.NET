# LiteRT.NET

C# / .NET and Unity bindings for [LiteRT](https://github.com/google-ai-edge/LiteRT) and
[LiteRT-LM](https://github.com/google-ai-edge/LiteRT-LM).

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
dotnet run --project examples/MinimalInference

dotnet build -c Release src/LiteRT.LM/LiteRT.LM.csproj
dotnet run --project examples/SimpleLlm -- /path/to/model.litertlm "Hello" cpu
```

Unity: open `examples/MinimalInferenceUnity` and run.

## Layout

```
src/LiteRT/             Core managed bindings + core native runtime (LiteRT; runtimes/<rid>/native)
src/LiteRT.LM/          LiteRT-LM managed bindings + LM native runtime (LiteRT.LM; runtimes/<rid>/native)
src/LiteRT.Gpu.*.Native/  Optional GPU accelerator packages (Metal / WebGpu / OpenCl)
unity/LiteRT/           Unity core UPM package: bindings (Runtime/Bindings) + natives (Plugins)
unity/LiteRT.Unity/     Unity utilities UPM package (LiteRtModelLoader)
examples/               Runnable samples (MinimalInference, SimpleLlm, MinimalInferenceUnity)
scripts/                Build/dev automation (fetch-natives, sync-unity-*, make-ios-xcframework)
.github/workflows/      CI (managed) and native build matrix
```
