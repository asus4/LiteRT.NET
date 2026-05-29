using System;
using System.Runtime.InteropServices;
using LiteRT.Interop;

namespace LiteRT
{
    /// <summary>A loaded LiteRT model (.tflite / .litert).</summary>
    public sealed class LiteRtModel : IDisposable
    {
        private IntPtr _handle;

        // When the model is created from an in-memory buffer the native runtime references
        // that buffer for the model's lifetime, so we pin it and release on Dispose.
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

        /// <summary>
        /// Creates a model from an in-memory <c>.tflite</c>/<c>.litert</c> buffer. Use this where
        /// no real file path exists — e.g. Unity on Android, where <c>StreamingAssets</c> lives
        /// inside the APK and must be read via <c>UnityWebRequest</c>. The buffer is pinned for the
        /// lifetime of the model (the native runtime references it, it does not copy), and released
        /// when <see cref="Dispose"/> is called.
        /// </summary>
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

        /// <summary>
        /// Creates a model from caller-owned native memory, without pinning or copying. The runtime
        /// references the buffer for the model's lifetime, so the memory at <paramref name="buffer"/>
        /// must stay valid and immovable until the returned model is disposed. The caller owns the
        /// memory — this method never frees it.
        /// <para>
        /// Use this to avoid the managed allocation of the <see cref="CreateFromBuffer(byte[])"/>
        /// overload for large models. For example, Unity's
        /// <c>NativeArray&lt;byte&gt;.ReadOnly</c> already lives in unmanaged memory; pass its
        /// <c>NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr</c> here (the
        /// <c>LiteRT.Unity</c> package wraps this in an extension method).
        /// </para>
        /// </summary>
        public static unsafe LiteRtModel CreateFromBuffer(IntPtr buffer, int length)
        {
            if (buffer == IntPtr.Zero) throw new ArgumentNullException(nameof(buffer));
            if (length <= 0) throw new ArgumentException("Model buffer is empty.", nameof(length));

            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtCreateModelFromBuffer((void*)buffer, (UIntPtr)length, out var handle),
                nameof(LiteRtNative.LiteRtCreateModelFromBuffer));
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
            if (_pinnedBuffer.IsAllocated)
            {
                _pinnedBuffer.Free();
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
