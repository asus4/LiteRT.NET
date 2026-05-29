using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LiteRT.Interop;

namespace LiteRT
{
    /// <summary>
    /// A LiteRT environment. Owns runtime/GPU context and must outlive any
    /// <see cref="LiteRtCompiledModel"/> and <see cref="LiteRtTensorBuffer"/> created from it.
    /// </summary>
    public sealed class LiteRtEnvironment : IDisposable
    {
        private IntPtr _handle;

        /// <param name="autoRegisterAccelerators">
        /// Which hardware accelerators the environment auto-registers (and probes plugins for)
        /// at creation. When <c>null</c> the runtime registers all supported accelerators,
        /// which on a CPU-only install logs warnings for the missing GPU/NPU plugins. Pass
        /// <see cref="LiteRtHwAccelerators.Cpu"/> for a quiet CPU-only environment, or include
        /// <see cref="LiteRtHwAccelerators.Gpu"/> to enable the GPU accelerator (requires the
        /// LiteRT.Gpu.Native package so the accelerator dylibs sit beside the core library).
        /// </param>
        public LiteRtEnvironment(LiteRtHwAccelerators? autoRegisterAccelerators = null)
        {
            var options = new List<LiteRtEnvOptionNative>();

            // Tell the runtime where the accelerator plugins live so it can dlopen them by
            // absolute path; otherwise it tries a bare filename the OS loader can't resolve.
            string? libraryDir = NativeRuntime.ResolveLibraryDirectory();
            IntPtr libraryDirPtr = IntPtr.Zero;
            try
            {
                if (libraryDir != null)
                {
                    libraryDirPtr = Marshal.StringToCoTaskMemUTF8(libraryDir);
                    options.Add(Option(LiteRtEnvOptionTag.RuntimeLibraryDir,
                        LiteRtAnyType.String, libraryDirPtr.ToInt64()));
                }

                if (autoRegisterAccelerators.HasValue)
                {
                    options.Add(Option(LiteRtEnvOptionTag.AutoRegisterAccelerators,
                        LiteRtAnyType.Int, (int)autoRegisterAccelerators.Value));
                }

                var array = options.ToArray();
                LiteRtException.ThrowIfError(
                    LiteRtNative.LiteRtCreateEnvironment(array.Length, array, out _handle),
                    nameof(LiteRtNative.LiteRtCreateEnvironment));
            }
            finally
            {
                if (libraryDirPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(libraryDirPtr);
                }
            }

            static LiteRtEnvOptionNative Option(LiteRtEnvOptionTag tag, LiteRtAnyType type, long value) =>
                new LiteRtEnvOptionNative
                {
                    Tag = (int)tag,
                    Value = new LiteRtAnyNative { Type = (int)type, Value = value },
                };
        }

        internal IntPtr Handle => _handle;

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                LiteRtNative.LiteRtDestroyEnvironment(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
