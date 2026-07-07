# LiteRT.NET

🚧 Work in Progress 🚧

C# / .NET and Unity bindings for [LiteRT](https://github.com/google-ai-edge/LiteRT) and [LiteRT-LM](https://github.com/google-ai-edge/LiteRT-LM).

## Download native libraries

```bash
# all RIDs
scripts/fetch-natives.sh
# Only host RID
LITERT_RIDS=osx-arm64 scripts/fetch-natives.sh
```

## Run examples

Minimal inference

```bash
dotnet run --project examples/MinimalInference
```

Simple LLM

```bash
# Initial setup:
scripts/litert-lm-c/build.sh /path/to/LiteRT-LM ./out
LITERT_RIDS=osx-arm64 scripts/fetch-natives.sh

dotnet run --project examples/SimpleChat -- /path/to/model.litertlm "Hello" cpu
```
