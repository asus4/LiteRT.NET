# LiteRT-LM for Unity

C# bindings for [LiteRT-LM](https://github.com/google-ai-edge/LiteRT-LM) — on-device LLM inference with `.litertlm` models (Gemma, Llama, Phi, Qwen, ...).

Part of [LiteRT.NET](https://github.com/asus4/LiteRT.NET). Depends on the core `com.koki-ibukuro.litert` package.

## Status

Experimental.

| Platform | Status |
| --- | --- |
| macOS (Apple Silicon) Editor/Standalone | ✅ |
| iOS (device arm64 + simulator arm64) | ✅ CPU backend |
| Android (arm64-v8a, x86_64; minSdk 24, IL2CPP) | ✅ CPU backend |
| Windows / Linux | ⏳ planned |

GPU backends on mobile are not wired up yet — use `backend: "cpu"` there.

## Usage

```csharp
using LiteRT.LM;

// Heavy: run off the main thread (seconds of native work).
var engine = LlmEngine.Create("/path/to/model.litertlm", backend: "cpu");
var conversation = engine.CreateConversation(new LlmConversationOptions
{
    SystemInstruction = "You are a helpful assistant.",
});

// Blocks until the final chunk; onTextChunk fires on a native background thread.
string reply = conversation.SendMessageStream("Hello!", chunk => { /* ... */ });

// Multi-turn: just keep sending. History lives in the conversation's KV cache.
string followUp = conversation.SendMessageStream("Tell me more.", chunk => { /* ... */ });

conversation.Dispose();
engine.Dispose();
```

For tool calls, channels, or multimodal content, use the raw JSON tier
(`SendMessageJson` / `SendMessageStreamJson`) with the message format described in
the LiteRT-LM docs. `LlmMessage` has helpers for building/parsing the JSON.

## Threading

- `LlmEngine.Create`, `SendMessage*` are **blocking** — call them from a background
  thread (e.g. after `Awaitable.BackgroundThreadAsync()`), never from the Unity main thread.
- Streaming callbacks (`onChunk`) are invoked on a **native background thread**.
  Do not touch Unity APIs inside them; hand chunks to the main thread (e.g. via a
  `ConcurrentQueue<string>` drained in an async loop).
- Cancel an in-flight generation with `Cancel()`, and always `Dispose()` the
  conversation before the engine in `OnDestroy` — an Editor domain reload while a
  stream is running can crash otherwise.

## Native libraries

`Plugins/` ships `libLiteRtLmC` (built by the LiteRT.NET repo — not prebuilt upstream)
and its load-time dependency `libGemmaModelConstraintProvider`:

- `macOS/`: dylibs; `backend: "gpu"` also uses the core package's Metal accelerator.
- `Android/arm64-v8a/`, `Android/x86_64/`: 16 KB-page aligned `.so`; requires IL2CPP +
  ARM64.
- `iOS/`: `LiteRtLmC.xcframework` + `GemmaModelConstraintProvider.xcframework`
  (min iOS 15), embedded dynamic frameworks resolved via `[DllImport("__Internal")]`.
