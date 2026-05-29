using System;
using System.IO;
using System.Runtime.InteropServices;
using LiteRT;
using UnityEngine;

namespace LiteRT.Unity
{
    /// <summary>
    /// Resolves the directory that holds the LiteRT native libraries shipped inside this UPM
    /// package's <c>Plugins/</c> folder, and hands it to the managed runtime.
    ///
    /// Unity does not use the .NET <c>deps.json</c> native-library search; it imports native
    /// libraries as plugins. The core <c>libLiteRt</c> resolves through Unity's own plugin loader,
    /// but the managed runtime still needs an absolute directory so it can <c>dlopen</c> accelerator
    /// plugins by path (required for GPU; harmless for CPU). This helper locates that directory and
    /// assigns it to <see cref="LiteRtRuntime.NativeLibraryDirectory"/> before the first
    /// <c>LiteRtEnvironment</c> is created.
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
                Debug.Log("[LiteRT.Unity] Could not locate the native library directory; " +
                          "relying on the default DllImport search. GPU accelerators may not load.");
            }
        }

        /// <summary>The platform-specific core native library file name.</summary>
        public static string CoreLibraryFileName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "LiteRt.dll"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libLiteRt.dylib"
                : "libLiteRt.so";
        }

        /// <summary>
        /// Locates the directory that contains the core native library, or <c>null</c> if it
        /// can't be found.
        /// </summary>
        public static string? ResolveNativeLibraryDirectory()
        {
            string core = CoreLibraryFileName();

#if UNITY_EDITOR
            // The core native libraries ship inside this package's Plugins/<platform> folder.
            // Resolve the package on disk (works for both file: and registry/cache installs) and
            // look in the host platform's subdirectory.
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
            // In players Unity places native plugins where the OS loader (and DllImport) resolve
            // them automatically. The absolute directory that accelerator dlopen needs is wired
            // per-platform in a later milestone (GPU / IL2CPP players). Probe common locations.
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
        // The Editor runs only on desktop hosts; map to the matching Plugins/ subdirectory.
        private static string EditorPluginSubdir()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows/x86_64"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS"
                : "Linux/x86_64";
        }
#endif
    }
}
