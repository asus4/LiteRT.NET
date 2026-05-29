# LiteRT.NET

🚧 Work in Progress 🚧

C# / .NET and Unity bindings for [LiteRT](https://github.com/google-ai-edge/LiteRT) and [LiteRT-LM](https://github.com/google-ai-edge/LiteRT-LM).

## Download native libraries

```bash
scripts/fetch-natives.sh            # all RIDs (best-effort)
LITERT_RIDS=osx-arm64 scripts/fetch-natives.sh   # just the host RID
```

## Run examples

Minimal inference

```bash
dotnet run --project examples/MinimalInference
```

Simple LLM

```bash
# Build the LM native library once (Bazel; see scripts/litert-lm-c/build.sh):
scripts/litert-lm-c/build.sh /path/to/LiteRT-LM ./out
# Then fetch-natives.sh copies it (and the Gemma plugin) into LiteRT.LM.Native:
LITERT_RIDS=osx-arm64 scripts/fetch-natives.sh

dotnet run --project examples/SimpleLlm -- /path/to/model.litertlm "Hello" cpu
```
