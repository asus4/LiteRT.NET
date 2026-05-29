using System;
using System.IO;
using System.Runtime.InteropServices;
using LiteRT;
using UnityEngine;

namespace LiteRT.Unity
{
    /// <summary>
    /// Unity-specific glue for the <c>LiteRT.Managed</c> NuGet package.
    ///
    /// Unity does not use the .NET <c>deps.json</c> native-library search; it imports native
    /// libraries as plugins under <c>Assets/</c> (in the Editor) or alongside the player (in a
    /// build). The managed runtime, however, still needs an absolute directory so it can
    /// <c>dlopen</c> accelerator plugins by path (required for GPU; harmless for CPU). This
    /// helper locates that directory and assigns it to
    /// <see cref="LiteRtRuntime.NativeLibraryDirectory"/> before the first
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

        /// <summary>
        /// The host runtime identifier (e.g. <c>osx-arm64</c>, <c>win-x64</c>) matching the
        /// <c>runtimes/&lt;rid&gt;/native</c> layout produced by the LiteRT.Native NuGet package.
        /// </summary>
        public static string HostRuntimeIdentifier()
        {
            string os =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
                : "linux";
            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
            };
            return $"{os}-{arch}";
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
            // In the Editor, NuGetForUnity extracts natives to
            // Assets/Packages/LiteRT.Native.<ver>/runtimes/<rid>/native/<core>.
            string rid = HostRuntimeIdentifier();
            string suffix = $"runtimes/{rid}/native";
            string assetsRoot = Application.dataPath; // <project>/Assets
            try
            {
                foreach (string path in Directory.EnumerateFiles(assetsRoot, core, SearchOption.AllDirectories))
                {
                    string dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
                    if (dir.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return dir;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LiteRT.Unity] Error scanning for native libraries: {e.Message}");
            }

            return null;
#else
            // In players, Unity places native plugins where the OS loader (and DllImport) can
            // resolve them automatically. The absolute directory that RuntimeLibraryDir needs
            // for accelerator plugins is platform-specific and is wired per-platform in a later
            // milestone (GPU / IL2CPP players). Probe a couple of common locations.
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
    }
}
