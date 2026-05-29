using System;
using System.Runtime.InteropServices;

namespace LiteRT.LM.Interop
{
    /// <summary>Sampler strategy (engine.h LiteRtLmSamplerType).</summary>
    public enum LiteRtLmSamplerType
    {
        Unspecified = 0,
        TopK = 1,
        TopP = 2,
        Greedy = 3,
    }

    /// <summary>Type of a single input data element (engine.h LiteRtLmInputDataType).</summary>
    public enum LiteRtLmInputDataType
    {
        Text = 0,
        Image = 1,
        ImageEnd = 2,
        Audio = 3,
        AudioEnd = 4,
    }

    /// <summary>Sampler parameters (engine.h LiteRtLmSamplerParams).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LiteRtLmSamplerParams
    {
        public LiteRtLmSamplerType Type;
        public int TopK;
        public float TopP;
        public float Temperature;
        public int Seed;
    }

    /// <summary>A single piece of input data (engine.h LiteRtLmInputData).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LiteRtLmInputData
    {
        public LiteRtLmInputDataType Type;
        public IntPtr Data;
        public UIntPtr Size;
    }

    /// <summary>Streaming callback (engine.h LiteRtLmStreamCallback).</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LiteRtLmStreamCallback(
        IntPtr callbackData,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? chunk,
        [MarshalAs(UnmanagedType.I1)] bool isFinal,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? errorMsg);

    /// <summary>
    /// Raw P/Invoke declarations for the LiteRT-LM C API (LiteRT-LM/c/engine.h),
    /// exported by the self-built <c>libLiteRtLmC</c> shared library.
    /// </summary>
    internal static class LiteRtLmNative
    {
        internal const string LibraryName = "LiteRtLmC";

        [DllImport(LibraryName)]
        internal static extern void litert_lm_set_min_log_level(int level);

        // --- Engine settings ---
        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_engine_settings_create(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string model_path,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string backend_str,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? vision_backend_str,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? audio_backend_str);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_engine_settings_delete(IntPtr settings);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_engine_settings_set_max_num_tokens(
            IntPtr settings, int max_num_tokens);

        // --- Engine ---
        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_engine_create(IntPtr settings);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_engine_delete(IntPtr engine);

        // --- Session config ---
        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_session_config_create();

        [DllImport(LibraryName)]
        internal static extern void litert_lm_session_config_delete(IntPtr config);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_session_config_set_max_output_tokens(
            IntPtr config, int max_output_tokens);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_session_config_set_sampler_params(
            IntPtr config, in LiteRtLmSamplerParams sampler_params);

        // --- Session ---
        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_engine_create_session(IntPtr engine, IntPtr config);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_session_delete(IntPtr session);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_session_cancel_process(IntPtr session);

        [DllImport(LibraryName)]
        internal static extern int litert_lm_session_run_prefill(
            IntPtr session, [In] LiteRtLmInputData[] inputs, UIntPtr num_inputs);

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_session_run_decode(IntPtr session);

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_session_generate_content(
            IntPtr session, [In] LiteRtLmInputData[] inputs, UIntPtr num_inputs);

        [DllImport(LibraryName)]
        internal static extern int litert_lm_session_generate_content_stream(
            IntPtr session, [In] LiteRtLmInputData[] inputs, UIntPtr num_inputs,
            LiteRtLmStreamCallback callback, IntPtr callback_data);

        [DllImport(LibraryName)]
        internal static extern int litert_lm_session_run_decode_async(
            IntPtr session, LiteRtLmStreamCallback callback, IntPtr callback_data);

        // --- Responses ---
        [DllImport(LibraryName)]
        internal static extern void litert_lm_responses_delete(IntPtr responses);

        [DllImport(LibraryName)]
        internal static extern int litert_lm_responses_get_num_candidates(IntPtr responses);

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_responses_get_response_text_at(IntPtr responses, int index);

        [DllImport(LibraryName)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool litert_lm_responses_has_score_at(IntPtr responses, int index);

        [DllImport(LibraryName)]
        internal static extern float litert_lm_responses_get_score_at(IntPtr responses, int index);

        // --- Tokenizer ---
        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_engine_tokenize(
            IntPtr engine, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_tokenize_result_delete(IntPtr result);

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_tokenize_result_get_tokens(IntPtr result);

        [DllImport(LibraryName)]
        internal static extern UIntPtr litert_lm_tokenize_result_get_num_tokens(IntPtr result);
    }
}
