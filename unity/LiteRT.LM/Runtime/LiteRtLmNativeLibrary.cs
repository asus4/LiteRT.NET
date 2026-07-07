using System.IO;
using UnityEngine;

namespace LiteRT.LM.Unity
{
    /// <summary>
    /// Sets <see cref="LiteRtRuntime.NativeLibraryDirectory"/> for the LM engine's GPU accelerator dlopen.
    /// Duplicates the core initializer because RuntimeInitializeOnLoadMethod ordering across assemblies is undefined.
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
            // Warn early if the LM natives were not synced (scripts/sync-unity-natives.sh).
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
