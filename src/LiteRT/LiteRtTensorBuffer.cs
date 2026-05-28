using System;
using LiteRT.Interop;

namespace LiteRT
{
    /// <summary>
    /// A managed tensor buffer used to feed inputs to and read outputs from a
    /// <see cref="LiteRtCompiledModel"/>.
    /// </summary>
    public sealed unsafe class LiteRtTensorBuffer : IDisposable
    {
        private IntPtr _handle;

        internal LiteRtTensorBuffer(IntPtr handle, LiteRtElementType elementType)
        {
            _handle = handle;
            ElementType = elementType;
        }

        internal IntPtr Handle => _handle;

        public LiteRtElementType ElementType { get; }

        /// <summary>Size in packed bytes used when reading/writing the locked buffer.</summary>
        public int PackedByteSize
        {
            get
            {
                LiteRtException.ThrowIfError(
                    LiteRtNative.LiteRtGetTensorBufferPackedSize(_handle, out var size),
                    nameof(LiteRtNative.LiteRtGetTensorBufferPackedSize));
                return checked((int)size);
            }
        }

        /// <summary>Copies <paramref name="data"/> into the buffer.</summary>
        public void Write(ReadOnlySpan<byte> data)
        {
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtLockTensorBuffer(_handle, out var ptr, LiteRtTensorBufferLockMode.Write),
                nameof(LiteRtNative.LiteRtLockTensorBuffer));
            try
            {
                var dst = new Span<byte>((void*)ptr, PackedByteSize);
                data.CopyTo(dst);
            }
            finally
            {
                LiteRtException.ThrowIfError(
                    LiteRtNative.LiteRtUnlockTensorBuffer(_handle),
                    nameof(LiteRtNative.LiteRtUnlockTensorBuffer));
            }
        }

        public void Write(ReadOnlySpan<float> data) =>
            Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(data));

        /// <summary>Copies the buffer contents into <paramref name="destination"/>.</summary>
        public void Read(Span<byte> destination)
        {
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtLockTensorBuffer(_handle, out var ptr, LiteRtTensorBufferLockMode.Read),
                nameof(LiteRtNative.LiteRtLockTensorBuffer));
            try
            {
                var src = new Span<byte>((void*)ptr, PackedByteSize);
                src.Slice(0, Math.Min(src.Length, destination.Length)).CopyTo(destination);
            }
            finally
            {
                LiteRtException.ThrowIfError(
                    LiteRtNative.LiteRtUnlockTensorBuffer(_handle),
                    nameof(LiteRtNative.LiteRtUnlockTensorBuffer));
            }
        }

        public float[] ReadFloats()
        {
            var bytes = new byte[PackedByteSize];
            Read(bytes);
            var result = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, result, 0, result.Length * sizeof(float));
            return result;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                LiteRtNative.LiteRtDestroyTensorBuffer(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
