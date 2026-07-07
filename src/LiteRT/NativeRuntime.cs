using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LiteRT
{
    public static class LiteRtRuntime
    {
        /// <summary>Overrides native-library probing; hosts without the deps.json layout (Unity
        /// imports natives as plugins) set this before the first <see cref="LiteRtEnvironment"/>.</summary>
        public static string? NativeLibraryDirectory { get; set; }
    }

    internal static class NativeRuntime
    {
        internal static string CoreLibraryFileName =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "LiteRt.dll"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libLiteRt.dylib"
            : "libLiteRt.so";

        internal static string? ResolveLibraryDirectory()
        {
            string core = CoreLibraryFileName;

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
