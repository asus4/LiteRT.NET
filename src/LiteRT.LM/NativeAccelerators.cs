using System;
using System.IO;
using System.Runtime.InteropServices;
using LiteRT;

namespace LiteRT.LM
{
    /// <summary>
    /// Pre-loads the LiteRT GPU accelerator and TopK sampler plugins for the LM engine.
    ///
    /// The LM engine creates its own internal LiteRT environment without a
    /// <c>RuntimeLibraryDir</c> option, so the core GPU registry and the LM sampler
    /// factory <c>dlopen</c> these plugins by their bare leaf name (e.g.
    /// <c>libLiteRtMetalAccelerator.dylib</c>, <c>libLiteRtTopKMetalSampler.dylib</c>).
    /// A bare name is not found in the app directory on macOS/Linux, so we load each
    /// plugin here by absolute path first; the later bare-name lookups then resolve to
    /// the already-loaded image. Without this, the accelerator fails to register and
    /// engine creation falls back to (or errors out of) the GPU path.
    /// </summary>
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

        // NativeLibrary is unavailable on netstandard2.1 (Unity/IL2CPP), so fall back to
        // the platform loader there. Loading by absolute path is enough: later bare-name
        // dlopen/LoadLibrary calls in the engine resolve to the already-loaded image.
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
            string baseDir = AppContext.BaseDirectory;
            yield return baseDir;
            yield return Path.Combine(baseDir, "runtimes", NativeRuntime.HostRid(), "native");
        }
    }
}
