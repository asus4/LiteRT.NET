using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LiteRT
{
    /// <summary>
    /// Runtime-wide configuration for locating the LiteRT native libraries. Hosts that do not
    /// use the standard .NET native-library search (deps.json <c>runtimes/&lt;rid&gt;/native</c>)
    /// can point the runtime at the directory holding the native libraries. Unity is the main
    /// case: it imports natives as plugins under <c>Assets/</c> rather than the deps.json layout,
    /// so <c>LiteRT.Unity</c> sets this before the first <see cref="LiteRtEnvironment"/> is
    /// created so accelerator plugins can be <c>dlopen</c>'d by absolute path.
    /// </summary>
    public static class LiteRtRuntime
    {
        /// <summary>
        /// Explicit directory containing the core native library (and any accelerator plugins).
        /// When set and valid, it takes precedence over automatic probing. <c>null</c> by default.
        /// </summary>
        public static string? NativeLibraryDirectory { get; set; }
    }

    /// <summary>
    /// Locates the directory that holds the LiteRT native libraries at runtime.
    /// The LiteRT runtime needs this path (via the <c>RuntimeLibraryDir</c> environment
    /// option) to <c>dlopen</c> its accelerator plugins by absolute path.
    /// </summary>
    internal static class NativeRuntime
    {
        internal static string CoreLibraryFileName =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "LiteRt.dll"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libLiteRt.dylib"
            : "libLiteRt.so";

        /// <summary>
        /// Returns the directory containing the core native library, or <c>null</c> if it
        /// cannot be found. Probes the app base directory (where ProjectReference and the
        /// packaged <c>build/*.targets</c> flatten the host-RID natives) and the
        /// <c>runtimes/&lt;rid&gt;/native</c> layout used by deps.json consumers.
        /// </summary>
        internal static string? ResolveLibraryDirectory()
        {
            string core = CoreLibraryFileName;

            // Explicit override (e.g. Unity sets the native plugin directory) wins.
            string? overrideDir = LiteRtRuntime.NativeLibraryDirectory;
            if (!string.IsNullOrEmpty(overrideDir) && File.Exists(Path.Combine(overrideDir!, core)))
            {
                return overrideDir;
            }

            string baseDir = AppContext.BaseDirectory;

            if (File.Exists(Path.Combine(baseDir, core)))
            {
                return baseDir;
            }

            string runtimesDir = Path.Combine(baseDir, "runtimes", HostRid(), "native");
            if (File.Exists(Path.Combine(runtimesDir, core)))
            {
                return runtimesDir;
            }

            return null;
        }

        internal static string HostRid()
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
    }
}
