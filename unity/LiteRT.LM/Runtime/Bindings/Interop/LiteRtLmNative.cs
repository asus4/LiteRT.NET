using System;
using System.Runtime.InteropServices;

namespace LiteRT.LM.Interop
{
    // engine.h LiteRtLmSamplerType
    public enum LiteRtLmSamplerType
    {
        Unspecified = 0,
        TopK = 1,
        TopP = 2,
        Greedy = 3,
    }

    // engine.h LiteRtLmInputDataType
    public enum LiteRtLmInputDataType
    {
        Text = 0,
        Image = 1,
        ImageEnd = 2,
        Audio = 3,
        AudioEnd = 4,
    }

    // engine.h LiteRtLmSamplerParams
    [StructLayout(LayoutKind.Sequential)]
    public struct LiteRtLmSamplerParams
    {
        public LiteRtLmSamplerType Type;
        public int TopK;
        public float TopP;
        public float Temperature;
        public int Seed;
    }

    // engine.h LiteRtLmInputData
    [StructLayout(LayoutKind.Sequential)]
    public struct LiteRtLmInputData
    {
        public LiteRtLmInputDataType Type;
        public IntPtr Data;
        public UIntPtr Size;
    }

    // engine.h LiteRtLmStreamCallback
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LiteRtLmStreamCallback(
        IntPtr callbackData,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? chunk,
        [MarshalAs(UnmanagedType.I1)] bool isFinal,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? errorMsg);

    // LiteRT-LM C API (LiteRT-LM/c/engine.h), exported by the self-built libLiteRtLmC.
    internal static class LiteRtLmNative
    {
        // iOS statically links plugins into the player binary, so P/Invoke must target "__Internal".
#if __IOS__ || (UNITY_IOS && !UNITY_EDITOR)
        internal const string LibraryName = "__Internal";
#else
        internal const string LibraryName = "LiteRtLmC";
#endif

        [DllImport(LibraryName)]
        internal static extern void litert_lm_set_min_log_level(int level);

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

        [DllImport(LibraryName)]
        internal static extern void litert_lm_engine_settings_set_activation_data_type(
            IntPtr settings, int activation_data_type_int);

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_engine_create(IntPtr settings);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_engine_delete(IntPtr engine);

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

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_conversation_config_create();

        [DllImport(LibraryName)]
        internal static extern void litert_lm_conversation_config_delete(IntPtr config);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_conversation_config_set_session_config(
            IntPtr config, IntPtr session_config);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_conversation_config_set_system_message(
            IntPtr config, [MarshalAs(UnmanagedType.LPUTF8Str)] string system_message_json);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_conversation_config_set_messages(
            IntPtr config, [MarshalAs(UnmanagedType.LPUTF8Str)] string messages_json);

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_conversation_optional_args_create();

        [DllImport(LibraryName)]
        internal static extern void litert_lm_conversation_optional_args_delete(IntPtr optional_args);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_conversation_optional_args_set_max_output_tokens(
            IntPtr optional_args, int max_output_tokens);

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_conversation_create(IntPtr engine, IntPtr config);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_conversation_delete(IntPtr conversation);

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_conversation_clone(IntPtr conversation);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_conversation_cancel_process(IntPtr conversation);

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_conversation_send_message(
            IntPtr conversation,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string message_json,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? extra_context,
            IntPtr optional_args);

        [DllImport(LibraryName)]
        internal static extern int litert_lm_conversation_send_message_stream(
            IntPtr conversation,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string message_json,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? extra_context,
            IntPtr optional_args,
            LiteRtLmStreamCallback callback, IntPtr callback_data);

        [DllImport(LibraryName)]
        internal static extern void litert_lm_json_response_delete(IntPtr response);

        [DllImport(LibraryName)]
        internal static extern IntPtr litert_lm_json_response_get_string(IntPtr response);

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
