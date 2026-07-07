using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using LiteRT.LM.Interop;

namespace LiteRT.LM
{
    /// <summary>A single inference session created from an <see cref="LlmEngine"/>.</summary>
    public sealed class LlmSession : IDisposable
    {
        private IntPtr _session;

        internal LlmSession(IntPtr session) => _session = session;

        /// <summary>Generates a full text response for a text prompt (blocking).</summary>
        public string GenerateContent(string prompt)
        {
            using var input = TextInput.Create(prompt);
            var responses = LiteRtLmNative.litert_lm_session_generate_content(
                _session, input.Array, input.Count);
            if (responses == IntPtr.Zero)
            {
                throw new InvalidOperationException("litert_lm_session_generate_content returned null.");
            }

            try
            {
                if (LiteRtLmNative.litert_lm_responses_get_num_candidates(responses) <= 0)
                {
                    return string.Empty;
                }
                var ptr = LiteRtLmNative.litert_lm_responses_get_response_text_at(responses, 0);
                return ptr == IntPtr.Zero ? string.Empty : (Marshal.PtrToStringUTF8(ptr) ?? string.Empty);
            }
            finally
            {
                LiteRtLmNative.litert_lm_responses_delete(responses);
            }
        }

        /// <summary>
        /// Generates a response, invoking <paramref name="onChunk"/> for each streamed chunk.
        /// Blocks until the stream completes and returns the full concatenated text.
        /// </summary>
        public string GenerateContentStream(string prompt, Action<string> onChunk)
        {
            if (onChunk == null) throw new ArgumentNullException(nameof(onChunk));

            using var context = new StreamCallbackBridge.Context(onChunk);
            var handle = StreamCallbackBridge.Pin(context);
            try
            {
                using var input = TextInput.Create(prompt);
                var status = LiteRtLmNative.litert_lm_session_generate_content_stream(
                    _session, input.Array, input.Count,
                    StreamCallbackBridge.Callback, GCHandle.ToIntPtr(handle));
                if (status != 0)
                {
                    throw new InvalidOperationException(
                        $"litert_lm_session_generate_content_stream failed with status {status}.");
                }
                return context.Wait();
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>Requests cancellation of an in-progress generation.</summary>
        public void Cancel() => LiteRtLmNative.litert_lm_session_cancel_process(_session);

        public void Dispose()
        {
            if (_session != IntPtr.Zero)
            {
                LiteRtLmNative.litert_lm_session_delete(_session);
                _session = IntPtr.Zero;
            }
        }

        /// <summary>Marshals a UTF-8 text prompt into an unmanaged input array.</summary>
        private readonly struct TextInput : IDisposable
        {
            private readonly IntPtr _text;
            public readonly LiteRtLmInputData[] Array;

            private TextInput(IntPtr text, LiteRtLmInputData[] array)
            {
                _text = text;
                Array = array;
            }

            public UIntPtr Count => (UIntPtr)Array.Length;

            public static TextInput Create(string prompt)
            {
                if (prompt == null) throw new ArgumentNullException(nameof(prompt));
                int byteCount = Encoding.UTF8.GetByteCount(prompt);
                IntPtr text = Marshal.StringToCoTaskMemUTF8(prompt);
                var array = new[]
                {
                    new LiteRtLmInputData
                    {
                        Type = LiteRtLmInputDataType.Text,
                        Data = text,
                        Size = (UIntPtr)byteCount,
                    },
                };
                return new TextInput(text, array);
            }

            public void Dispose()
            {
                if (_text != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(_text);
                }
            }
        }
    }
}
