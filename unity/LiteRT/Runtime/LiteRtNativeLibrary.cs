using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LiteRT.Unity
{
    /// <summary>
    /// Sets <see cref="LiteRtRuntime.NativeLibraryDirectory"/> to this package's Plugins/ natives:
    /// the runtime dlopens accelerator plugins by absolute path (required for GPU).
    /// </summary>
    public static class LiteRtNativeLibrary
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (!string.IsNullOrEmpty(LiteRtRuntime.NativeLibraryDirectory))
            {
                return; // Respect an explicit override set by user code.
            }

            string? dir = ResolveNativeLibraryDirectory();
            if (!string.IsNullOrEmpty(dir))
            {
                LiteRtRuntime.NativeLibraryDirectory = dir;
                Debug.Log($"[LiteRT.Unity] Native library directory: {dir}");
            }
            else
            {
                Debug.Log("[LiteRT.Unity] Could not locate the native library directory.");
            }
        }

        public static string CoreLibraryFileName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "LiteRt.dll"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libLiteRt.dylib"
                : "libLiteRt.so";
        }

        public static string? ResolveNativeLibraryDirectory()
        {
            string core = CoreLibraryFileName();

#if UNITY_EDITOR
            // Resolving the package on disk works for both file: and registry/cache installs.
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(LiteRtNativeLibrary).Assembly);
            if (package != null)
            {
                string dir = Path.Combine(package.resolvedPath, "Plugins", EditorPluginSubdir());
                if (File.Exists(Path.Combine(dir, core)))
                {
                    return dir.Replace('\\', '/');
                }
            }

            return null;
#else
            // Players: per-platform wiring for accelerator dlopen comes later; probe common locations.
            string[] candidates =
            {
                Path.Combine(Application.dataPath, "Plugins"),
                Application.dataPath,
            };
            foreach (string dir in candidates)
            {
                if (File.Exists(Path.Combine(dir, core)))
                {
                    return dir;
                }
            }

            return null;
#endif
        }

#if UNITY_EDITOR
        private static string EditorPluginSubdir()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows/x86_64"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS"
                : "Linux/x86_64";
        }
#endif
    }
}
