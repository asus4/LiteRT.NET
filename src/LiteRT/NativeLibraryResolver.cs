using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LiteRT
{
    /// <summary>
    /// Maps logical native library names (e.g. "LiteRt", "LiteRtLmC") to the
    /// platform-specific file that ships in the prebuilt binaries, and lets a
    /// development directory be injected via the <c>LITERT_NATIVE_DIR</c> environment
    /// variable. Registered per-assembly via <see cref="NativeLibrary.SetDllImportResolver"/>.
    /// </summary>
    public static class NativeLibraryResolver
    {
        private static readonly object Gate = new object();
        private static readonly HashSet<Assembly> Registered = new HashSet<Assembly>();

        /// <summary>Registers the resolver for the LiteRT core assembly (idempotent).</summary>
        public static void EnsureRegistered() => Register(typeof(NativeLibraryResolver).Assembly);

        /// <summary>
        /// Registers the resolver for the given assembly (idempotent).
        /// On netstandard2.1 (Unity) this is a no-op: the host loads native plugins itself.
        /// </summary>
        public static void Register(Assembly assembly)
        {
            lock (Gate)
            {
                if (!Registered.Add(assembly))
                {
                    return;
                }
#if NET5_0_OR_GREATER
                NativeLibrary.SetDllImportResolver(assembly, Resolve);
#endif
            }
        }

#if NET5_0_OR_GREATER
        private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            foreach (var candidate in CandidateFileNames(libraryName))
            {
                foreach (var dir in SearchDirectories())
                {
                    var path = Path.Combine(dir, candidate);
                    if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
                    {
                        return handle;
                    }
                }

                // Let the OS loader resolve the bare candidate name too.
                if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var osHandle))
                {
                    return osHandle;
                }
            }

            return IntPtr.Zero;
        }

        private static IEnumerable<string> CandidateFileNames(string name)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                yield return "lib" + name + ".dylib";
                yield return name + ".dylib";
                // Bazel emits .so even on macOS; accept it as a fallback.
                yield return "lib" + name + ".so";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                yield return name + ".dll";
                yield return "lib" + name + ".dll";
            }
            else
            {
                yield return "lib" + name + ".so";
                yield return name + ".so";
            }
        }

        private static IEnumerable<string> SearchDirectories()
        {
            var overrideDir = Environment.GetEnvironmentVariable("LITERT_NATIVE_DIR");
            if (!string.IsNullOrEmpty(overrideDir))
            {
                yield return overrideDir!;
            }

            var baseDir = AppContext.BaseDirectory;
            yield return baseDir;

            yield return Path.Combine(baseDir, "runtimes", GetRuntimeIdentifier(), "native");
        }

        private static string GetRuntimeIdentifier()
        {
            string os;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                os = "osx";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                os = "win";
            }
            else
            {
                os = "linux";
            }

            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => "x64",
            };

            return os + "-" + arch;
        }
#endif
    }
}
