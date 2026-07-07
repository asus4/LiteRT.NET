using System;
using System.Runtime.InteropServices;

namespace LiteRT.Interop
{
    // litert_common.h
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

    // litert_common.h
    [Flags]
    public enum LiteRtHwAccelerators
    {
        None = 0,
        Cpu = 1 << 0,
        Gpu = 1 << 1,
        Npu = 1 << 2,
    }

    // litert_common.h
    public enum LiteRtTensorBufferLockMode
    {
        Read = 0,
        Write = 1,
        ReadWrite = 2,
    }

    // litert_environment_options.h
    public enum LiteRtEnvOptionTag
    {
        RuntimeLibraryDir = 22,
        AutoRegisterAccelerators = 24,
    }

    // litert_any.h
    public enum LiteRtAnyType
    {
        None = 0,
        Bool = 1,
        Int = 2,
        Real = 3,
        String = 8,
        VoidPtr = 9,
    }

    // LiteRtAny (litert_any.h): 4-byte tag, 4 padding, 8-byte union (int64/double/pointer).
    [StructLayout(LayoutKind.Sequential)]
    internal struct LiteRtAnyNative
    {
        public int Type;
        private int _padding;
        public long Value;
    }

    // LiteRtEnvOption (litert_environment_options.h): 4-byte tag, 4 padding, 16-byte LiteRtAnyNative.
    [StructLayout(LayoutKind.Sequential)]
    internal struct LiteRtEnvOptionNative
    {
        public int Tag;
        private int _padding;
        public LiteRtAnyNative Value;
    }

    // litert_model_types.h
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

    // LiteRT core C API (litert/c/*.h), exported by libLiteRt. LiteRtRankedTensorType is
    // passed as an opaque RankedTensorTypeSize-byte blob to dodge cross-compiler bitfield ABI.
    internal static unsafe class LiteRtNative
    {
        // iOS resolves symbols from the loaded image, so P/Invoke must target "__Internal".
        // UNITY_IOS covers Unity device builds (Unity doesn't define __IOS__); plain dotnet builds keep "LiteRt".
#if __IOS__ || (UNITY_IOS && !UNITY_EDITOR)
        internal const string LibraryName = "__Internal";
#else
        internal const string LibraryName = "LiteRt";
#endif

        // sizeof(LiteRtRankedTensorType) is 72 or 76 depending on compiler; 128 is a safe upper bound.
        internal const int RankedTensorTypeSize = 128;

        [DllImport(LibraryName)]
        internal static extern IntPtr LiteRtGetStatusString(LiteRtStatus status);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtCreateEnvironment(
            int num_options, [In] LiteRtEnvOptionNative[] options, out IntPtr environment);

        [DllImport(LibraryName)]
        internal static extern void LiteRtDestroyEnvironment(IntPtr environment);

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

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetRankedTensorType(
            IntPtr tensor, byte* ranked_tensor_type);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtCreateOptions(out IntPtr options);

        [DllImport(LibraryName)]
        internal static extern void LiteRtDestroyOptions(IntPtr options);

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtSetOptionsHardwareAccelerators(
            IntPtr options, int hardware_accelerators);

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

        [DllImport(LibraryName)]
        internal static extern LiteRtStatus LiteRtGetTensorBufferRequirementsBufferSize(
            IntPtr requirements, out UIntPtr buffer_size);

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
