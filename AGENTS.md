# LiteRT.NET

C# / .NET and Unity bindings for [LiteRT](https://github.com/google-ai-edge/LiteRT) and
[LiteRT-LM](https://github.com/google-ai-edge/LiteRT-LM).

## Reproduce

Native binaries are generated, not committed — populate them first: run
`scripts/fetch-natives.sh` (and `scripts/litert-lm-c/build.sh` for the LiteRT-LM C API,
which isn't prebuilt upstream). See [README.md](README.md) for the exact commands.

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
dotnet run --project examples/SimpleChat -- /path/to/model.litertlm "Hello" cpu
```

Unity: open `examples/MinimalInferenceUnity` and run.

## Layout

```
src/LiteRT/             Core managed bindings + core native runtime (LiteRT; runtimes/<rid>/native)
src/LiteRT.LM/          LiteRT-LM managed bindings + LM native runtime (LiteRT.LM; runtimes/<rid>/native)
src/LiteRT.Gpu.*/       Optional GPU accelerator packages (Metal / WebGpu / OpenCl)
unity/LiteRT/           Unity core UPM package: bindings (Runtime/Bindings) + natives (Plugins)
unity/LiteRT.Unity/     Unity utilities UPM package (LiteRtModelLoader)
examples/               Runnable samples (MinimalInference, SimpleChat, MinimalInferenceUnity)
scripts/                Build/dev automation (fetch-natives, sync-unity-*, make-ios-xcframework)
.github/workflows/      CI (managed) and native build matrix
```
