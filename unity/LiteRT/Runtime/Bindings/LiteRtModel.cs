using System;
using System.Runtime.InteropServices;
using LiteRT.Interop;

namespace LiteRT
{
    /// <summary>A loaded LiteRT model (.tflite / .litert).</summary>
    public sealed class LiteRtModel : IDisposable
    {
        private IntPtr _handle;

        // Buffer-backed models: the native runtime references (not copies) the buffer,
        // so it stays pinned until Dispose.
        private GCHandle _pinnedBuffer;

        private LiteRtModel(IntPtr handle, GCHandle pinnedBuffer = default)
        {
            _handle = handle;
            _pinnedBuffer = pinnedBuffer;
        }

        internal IntPtr Handle => _handle;

        public static LiteRtModel CreateFromFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtCreateModelFromFile(path, out var handle),
                nameof(LiteRtNative.LiteRtCreateModelFromFile));
            return new LiteRtModel(handle);
        }

        /// <summary>The native runtime references (not copies) the buffer; it stays pinned until Dispose.</summary>
        public static unsafe LiteRtModel CreateFromBuffer(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("Model buffer is empty.", nameof(data));

            var pinned = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                void* ptr = (void*)pinned.AddrOfPinnedObject();
                LiteRtException.ThrowIfError(
                    LiteRtNative.LiteRtCreateModelFromBuffer(ptr, (UIntPtr)data.Length, out var handle),
                    nameof(LiteRtNative.LiteRtCreateModelFromBuffer));
                return new LiteRtModel(handle, pinned);
            }
            catch
            {
                pinned.Free();
                throw;
            }
        }

        /// <summary>Caller-owned native memory: must stay valid and immovable until the model is
        /// disposed; never freed by this class.</summary>
        public static unsafe LiteRtModel CreateFromBuffer(IntPtr buffer, int length)
        {
            if (buffer == IntPtr.Zero) throw new ArgumentNullException(nameof(buffer));
            if (length <= 0) throw new ArgumentException("Model buffer is empty.", nameof(length));

            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtCreateModelFromBuffer((void*)buffer, (UIntPtr)length, out var handle),
                nameof(LiteRtNative.LiteRtCreateModelFromBuffer));
            return new LiteRtModel(handle);
        }

        public int SignatureCount
        {
            get
            {
                LiteRtException.ThrowIfError(
                    LiteRtNative.LiteRtGetNumModelSignatures(_handle, out var num),
                    nameof(LiteRtNative.LiteRtGetNumModelSignatures));
                return checked((int)num);
            }
        }

        public LiteRtSignature GetSignature(int index)
        {
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtGetModelSignature(_handle, (UIntPtr)index, out var sig),
                nameof(LiteRtNative.LiteRtGetModelSignature));
            return new LiteRtSignature(sig, index);
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                LiteRtNative.LiteRtDestroyModel(_handle);
                _handle = IntPtr.Zero;
            }
            if (_pinnedBuffer.IsAllocated)
            {
                _pinnedBuffer.Free();
            }
        }
    }

    public sealed class LiteRtSignature
    {
        internal IntPtr Handle { get; }
        public int Index { get; }

        internal LiteRtSignature(IntPtr handle, int index)
        {
            Handle = handle;
            Index = index;
        }

        public int InputCount
        {
            get
            {
                LiteRtException.ThrowIfError(
                    LiteRtNative.LiteRtGetNumSignatureInputs(Handle, out var num),
                    nameof(LiteRtNative.LiteRtGetNumSignatureInputs));
                return checked((int)num);
            }
        }

        public int OutputCount
        {
            get
            {
                LiteRtException.ThrowIfError(
                    LiteRtNative.LiteRtGetNumSignatureOutputs(Handle, out var num),
                    nameof(LiteRtNative.LiteRtGetNumSignatureOutputs));
                return checked((int)num);
            }
        }

        public string GetInputName(int index)
        {
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtGetSignatureInputName(Handle, (UIntPtr)index, out var ptr),
                nameof(LiteRtNative.LiteRtGetSignatureInputName));
            return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
        }

        public string GetOutputName(int index)
        {
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtGetSignatureOutputName(Handle, (UIntPtr)index, out var ptr),
                nameof(LiteRtNative.LiteRtGetSignatureOutputName));
            return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
        }

        public LiteRtRankedTensorType GetInputTensorType(int index) =>
            LiteRtRankedTensorType.FromTensor(GetInputTensor(index));

        public LiteRtRankedTensorType GetOutputTensorType(int index) =>
            LiteRtRankedTensorType.FromTensor(GetOutputTensor(index));

        internal IntPtr GetInputTensor(int index)
        {
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtGetSignatureInputTensorByIndex(Handle, (UIntPtr)index, out var tensor),
                nameof(LiteRtNative.LiteRtGetSignatureInputTensorByIndex));
            return tensor;
        }

        internal IntPtr GetOutputTensor(int index)
        {
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtGetSignatureOutputTensorByIndex(Handle, (UIntPtr)index, out var tensor),
                nameof(LiteRtNative.LiteRtGetSignatureOutputTensorByIndex));
            return tensor;
        }
    }
}
