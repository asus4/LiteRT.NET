# Changelog

All notable changes to this package will be documented in this file.

## [0.1.0-alpha] - 2026-07-07

- Initial release: LiteRT-LM bindings for Unity (LlmEngine, LlmSession, LlmConversation).
- macOS (Apple Silicon) natives: libLiteRtLmC + Gemma model constraint provider.
- iOS natives (device + simulator arm64, min iOS 15) as xcframework plugins that Unity
  links into UnityFramework and embeds in the app.
- Android natives (arm64-v8a, x86_64; 16 KB-page aligned; minSdk 24, IL2CPP).
