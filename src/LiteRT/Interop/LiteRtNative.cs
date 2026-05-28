using System;
using System.Runtime.InteropServices;

namespace LiteRT.Interop
{
    /// <summary>Status codes returned by the LiteRT C API (litert_common.h).</summary>
    public enum LiteRtStatus
    {
        Ok = 0,
        ErrorInvalidArgument = 1,
        ErrorMemoryAllocationFailure = 2,
        ErrorRuntimeFailure = 3,
        ErrorMissingInputTensor = 4,
        ErrorUnsupported = 5,
        ErrorNotFound = 6,
        ErrorTimeoutExpired = 7,
        ErrorWrongVersion = 8,
        ErrorUnknown = 9,
        ErrorAlreadyExists = 10,
        Cancelled = 100,
        ErrorFileIO = 500,
        ErrorInvalidFlatbuffer = 501,
        ErrorDynamicLoading = 502,
        ErrorSerialization = 503,
        ErrorCompilation = 504,
    }

    /// <summary>Hardware accelerator bit flags (litert_common.h).</summary>
    [Flags]
    public enum LiteRtHwAccelerators
    {
        None = 0,
        Cpu = 1 << 0,
        Gpu = 1 << 1,
        Npu = 1 << 2,
    }

    /// <summary>Lock mode for a tensor buffer (litert_common.h).</summary>
    public enum LiteRtTensorBufferLockMode
    {
        Read = 0,
        Write = 1,
        ReadWrite = 2,
    }

    /// <summary>Element type of a tensor (litert_model_types.h).</summary>
    public enum LiteRtElementType
    {
        None = 0,
        Bool = 6,
        Int2 = 20,
        Int4 = 18,
        Int8 = 9,
        Int16 = 7,
        Int32 = 2,
        Int64 = 4,
        UInt8 = 3,
        UInt16 = 17,
        UInt32 = 16,
        UInt64 = 13,
        Float16 = 10,
        BFloat16 = 19,
        Float32 = 1,
        Float64 = 11,
        Complex64 = 8,
        Complex128 = 12,
    }

    /// <summary>
    /// Raw P/Invoke declarations for the LiteRT core C API (litert/c/*.h),
    /// exported by libLiteRt. Opaque handles are represented as <see cref="IntPtr"/>.
    /// <c>LiteRtParamIndex</c> (size_t) maps to <see cref="UIntPtr"/>.
    /// The <c>LiteRtRankedTensorType</c> struct is treated as an opaque blob
    /// (see <see cref="RankedTensorTypeSize"/>) to avoid cross-platform ABI bitfield issues.
    /// </summary>
    internal static unsafe class LiteRtNative
    {
        internal const string LibraryName = "LiteRt";

        // sizeof(LiteRtRankedTensorType): 4 (element_type) + sizeof(LiteRtLayout).
        // LiteRtLayout is 68 bytes (non-MSVC) or 72 (MSVC). 128 is a safe upper bound.
        internal const int RankedTensorTypeSize = 128;

        static LiteRtNative() => NativeLibraryResolver.EnsureRegistered();

        [DllImport(LibraryName)]
        internal static extern IntPtr LiteRtGetStatusString(LiteRtStatus status);

        // --- Environment ---
        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtCreateEnvironment(
            int num_options, IntPtr options, out IntPtr environment);

        [DllImport(LibraryName)]
        internal static extern void LiteRtDestroyEnvironment(IntPtr environment);

        // --- Model ---
        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtCreateModelFromFile(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string filename, out IntPtr model);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtCreateModelFromBuffer(
            void* buffer_addr, UIntPtr buffer_size, out IntPtr model);

        [DllImport(LibraryName)]
        internal static extern void LiteRtDestroyModel(IntPtr model);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetNumModelSignatures(
            IntPtr model, out UIntPtr num_signatures);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetModelSignature(
            IntPtr model, UIntPtr signature_index, out IntPtr signature);

        // --- Signature ---
        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetNumSignatureInputs(
            IntPtr signature, out UIntPtr num_inputs);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetSignatureInputName(
            IntPtr signature, UIntPtr input_idx, out IntPtr input_name);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetSignatureInputTensorByIndex(
            IntPtr signature, UIntPtr input_idx, out IntPtr tensor);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetNumSignatureOutputs(
            IntPtr signature, out UIntPtr num_outputs);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetSignatureOutputName(
            IntPtr signature, UIntPtr output_idx, out IntPtr output_name);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetSignatureOutputTensorByIndex(
            IntPtr signature, UIntPtr output_idx, out IntPtr tensor);

        // --- Tensor ---
        // ranked_tensor_type points to an opaque RankedTensorTypeSize-byte buffer.
        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetRankedTensorType(
            IntPtr tensor, byte* ranked_tensor_type);

        // --- Options ---
        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtCreateOptions(out IntPtr options);

        [DllImport(LibraryName)]
        internal static extern void LiteRtDestroyOptions(IntPtr options);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtSetOptionsHardwareAccelerators(
            IntPtr options, int hardware_accelerators);

        // --- CompiledModel ---
        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtCreateCompiledModel(
            IntPtr environment, IntPtr model, IntPtr compilation_options,
            out IntPtr compiled_model);

        [DllImport(LibraryName)]
        internal static extern void LiteRtDestroyCompiledModel(IntPtr compiled_model);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetCompiledModelInputBufferRequirements(
            IntPtr compiled_model, UIntPtr signature_index, UIntPtr input_index,
            out IntPtr buffer_requirements);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetCompiledModelOutputBufferRequirements(
            IntPtr compiled_model, UIntPtr signature_index, UIntPtr output_index,
            out IntPtr buffer_requirements);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtRunCompiledModel(
            IntPtr compiled_model, UIntPtr signature_index,
            UIntPtr num_input_buffers, IntPtr[] input_buffers,
            UIntPtr num_output_buffers, IntPtr[] output_buffers);

        // --- TensorBufferRequirements ---
        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetTensorBufferRequirementsBufferSize(
            IntPtr requirements, out UIntPtr buffer_size);

        // --- TensorBuffer ---
        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtCreateManagedTensorBufferFromRequirements(
            IntPtr env, byte* tensor_type, IntPtr requirements, out IntPtr buffer);

        [DllImport(LibraryName)]
        internal static extern void LiteRtDestroyTensorBuffer(IntPtr buffer);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetTensorBufferPackedSize(
            IntPtr tensor_buffer, out UIntPtr size);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtLockTensorBuffer(
            IntPtr tensor_buffer, out IntPtr host_mem_addr, LiteRtTensorBufferLockMode lock_mode);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtUnlockTensorBuffer(IntPtr buffer);
    }
}
