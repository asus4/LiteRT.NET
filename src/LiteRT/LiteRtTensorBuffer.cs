using System;
using System.Runtime.InteropServices;
using LiteRT.Interop;

namespace LiteRT
{
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
            Write(MemoryMarshal.AsBytes(data));

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
            var result = new float[PackedByteSize / sizeof(float)];
            Read(MemoryMarshal.AsBytes(result.AsSpan()));
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
