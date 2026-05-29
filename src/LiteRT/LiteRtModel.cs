using System;
using System.Runtime.InteropServices;
using LiteRT.Interop;

namespace LiteRT
{
    /// <summary>A loaded LiteRT model (.tflite / .litert).</summary>
    public sealed class LiteRtModel : IDisposable
    {
        private IntPtr _handle;

        private LiteRtModel(IntPtr handle) => _handle = handle;

        internal IntPtr Handle => _handle;

        public static LiteRtModel CreateFromFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtCreateModelFromFile(path, out var handle),
                nameof(LiteRtNative.LiteRtCreateModelFromFile));
            return new LiteRtModel(handle);
        }

        /// <summary>Number of signatures exposed by the model.</summary>
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

        /// <summary>Returns metadata for the signature at <paramref name="index"/>.</summary>
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
        }
    }

    /// <summary>Describes the inputs/outputs of one model signature.</summary>
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
