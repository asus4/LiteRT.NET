using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LiteRT
{
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

        private static string HostRid()
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
