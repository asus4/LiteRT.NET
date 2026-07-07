using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LiteRT.Interop;

namespace LiteRT
{
    /// <summary>Must outlive any <see cref="LiteRtCompiledModel"/> and <see cref="LiteRtTensorBuffer"/> created from it.</summary>
    public sealed class LiteRtEnvironment : IDisposable
    {
        private IntPtr _handle;

        /// <param name="autoRegisterAccelerators">null registers all accelerators (logs warnings
        /// for missing GPU/NPU plugins); pass Cpu for a quiet CPU-only environment.</param>
        public LiteRtEnvironment(LiteRtHwAccelerators? autoRegisterAccelerators = null)
        {
            var options = new List<LiteRtEnvOptionNative>();

            // Accelerator plugins must be dlopen'd by absolute path; a bare filename fails.
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
