using System;
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

        public LiteRtEnvironment()
        {
            NativeLibraryResolver.EnsureRegistered();
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtCreateEnvironment(0, IntPtr.Zero, out _handle),
                nameof(LiteRtNative.LiteRtCreateEnvironment));
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
