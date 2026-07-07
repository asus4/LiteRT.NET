# LiteRT.NET

🚧 Work in Progress 🚧

C# / .NET and Unity bindings for [LiteRT](https://github.com/google-ai-edge/LiteRT) and [LiteRT-LM](https://github.com/google-ai-edge/LiteRT-LM).

## Download native libraries

Native binaries are generated, not committed:

```bash
# all RIDs (best-effort)
scripts/fetch-natives.sh
# Only host RID
LITERT_RIDS=osx-arm64 scripts/fetch-natives.sh
```

`libLiteRtLmC` (the LiteRT-LM C API) isn't prebuilt upstream — build it once for the host, then re-run `fetch-natives.sh`:

```bash
scripts/litert-lm-c/build.sh /path/to/LiteRT-LM ./out
```

## Run examples

```bash
# Minimal inference
dotnet run --project examples/MinimalInference

# Simple LLM (requires libLiteRtLmC, see above)
dotnet run --project examples/SimpleChat -- /path/to/model.litertlm "Hello" cpu
```

See [examples/README.md](examples/README.md) for GPU options and the Unity sample.

## Docs

- [Publishing the Unity packages to OpenUPM](docs/openupm-publishing.md)
- [GPU accelerator probe order](docs/gpu-accelerator-probe-order.md)
