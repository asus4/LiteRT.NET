using System.IO;
using UnityEngine;

namespace LiteRT.LM.Unity
{
    /// <summary>
    /// Wires the LiteRT-LM natives shipped in this package into the managed runtime.
    ///
    /// <c>libLiteRtLmC</c> itself resolves through Unity's plugin importer (DllImport by
    /// bare name), and its load-time dependency <c>libGemmaModelConstraintProvider</c>
    /// resolves via the <c>@loader_path</c> rpath baked into the dylib. What still needs
    /// wiring is <see cref="LiteRtRuntime.NativeLibraryDirectory"/>: the LM engine's GPU
    /// path dlopens accelerator plugins from that directory. The core package's own
    /// initializer usually sets it, but <c>RuntimeInitializeOnLoadMethod</c> ordering
    /// across assemblies is undefined, so this initializer resolves it too (idempotent).
    /// </summary>
    public static class LiteRtLmNativeLibrary
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (string.IsNullOrEmpty(LiteRtRuntime.NativeLibraryDirectory))
            {
                string? dir = LiteRT.Unity.LiteRtNativeLibrary.ResolveNativeLibraryDirectory();
                if (!string.IsNullOrEmpty(dir))
                {
                    LiteRtRuntime.NativeLibraryDirectory = dir;
                }
            }

#if UNITY_EDITOR
            // Sanity check: warn early if the LM natives were not synced into this package
            // (scripts/sync-unity-natives.sh) instead of failing at the first P/Invoke.
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(LiteRtLmNativeLibrary).Assembly);
            if (package != null)
            {
                string dylib = Path.Combine(package.resolvedPath, "Plugins", "macOS", "libLiteRtLmC.dylib");
                if (Application.platform == RuntimePlatform.OSXEditor && !File.Exists(dylib))
                {
                    Debug.LogWarning(
                        "[LiteRT.LM] libLiteRtLmC.dylib is missing from the package Plugins folder. " +
                        "Run scripts/sync-unity-natives.sh in the LiteRT.NET repository.");
                }
            }
#endif
        }
    }
}
