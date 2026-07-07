using System;
using System.IO;
using System.Runtime.InteropServices;
using LiteRT;

namespace LiteRT.LM
{
    // The LM engine's internal LiteRT environment has no RuntimeLibraryDir, so it dlopens GPU
    // accelerator/sampler plugins by bare leaf name, which the OS loader can't resolve.
    // Preloading by absolute path makes those bare-name lookups hit the already-loaded images.
    internal static class NativeAccelerators
    {
        private static bool _loaded;

        internal static void PreloadGpu()
        {
            if (_loaded) return;
            _loaded = true;

            foreach (string dir in CandidateDirectories())
            {
                if (!Directory.Exists(dir)) continue;
                foreach (string path in Directory.GetFiles(dir))
                {
                    if (!IsGpuPlugin(Path.GetFileName(path))) continue;
                    try { LoadByAbsolutePath(path); }
                    catch { /* best effort: missing/incompatible plugins are non-fatal */ }
                }
            }
        }

        // NativeLibrary is unavailable on netstandard2.1 (Unity/IL2CPP); fall back to the platform loader.
        private static void LoadByAbsolutePath(string path)
        {
#if NET6_0_OR_GREATER
            NativeLibrary.Load(path);
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (LoadLibraryW(path) == IntPtr.Zero)
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            else if (dlopen(path, RTLD_NOW) == IntPtr.Zero)
            {
                throw new InvalidOperationException("dlopen failed for " + path);
            }
#endif
        }

#if !NET6_0_OR_GREATER
        private const int RTLD_NOW = 2;

        [DllImport("libdl", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen(string fileName, int flags);

        [DllImport("kernel32", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string fileName);
#endif

        private static bool IsGpuPlugin(string fileName)
        {
            bool isPlugin = fileName.IndexOf("Accelerator", StringComparison.Ordinal) >= 0
                || fileName.IndexOf("Sampler", StringComparison.Ordinal) >= 0;
            if (!isPlugin) return false;
            return fileName.EndsWith(".dylib", StringComparison.Ordinal)
                || fileName.EndsWith(".so", StringComparison.Ordinal)
                || fileName.EndsWith(".dll", StringComparison.Ordinal);
        }

        private static System.Collections.Generic.IEnumerable<string> CandidateDirectories()
        {
            string? overrideDir = LiteRtRuntime.NativeLibraryDirectory;
            if (!string.IsNullOrEmpty(overrideDir))
            {
                yield return overrideDir!;
            }

            string baseDir = AppContext.BaseDirectory;
            yield return baseDir;
            yield return Path.Combine(baseDir, "runtimes", NativeRuntime.HostRid(), "native");
        }
    }
}
