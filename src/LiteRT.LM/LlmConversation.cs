using System;
using System.Runtime.InteropServices;
using LiteRT.LM.Interop;

namespace LiteRT.LM
{
    /// <summary>
    /// A multi-turn conversation created from an <see cref="LlmEngine"/>. The conversation
    /// applies the model's prompt template, keeps the dialogue history in the KV cache,
    /// and exchanges messages as JSON (see <see cref="LlmMessage"/> for the format).
    ///
    /// The <c>*Json</c> methods are the lossless tier (tool calls, channels, multimodal
    /// content); <see cref="SendMessage"/> / <see cref="SendMessageStream"/> are text-only
    /// conveniences. Streaming callbacks are invoked on a native background thread.
    /// </summary>
    public sealed class LlmConversation : IDisposable
    {
        private IntPtr _conversation;

        internal LlmConversation(IntPtr conversation) => _conversation = conversation;

        /// <summary>Sends a message JSON and returns the raw response JSON (blocking).</summary>
        public string SendMessageJson(string messageJson, string? extraContextJson = null)
        {
            if (messageJson == null) throw new ArgumentNullException(nameof(messageJson));

            var response = LiteRtLmNative.litert_lm_conversation_send_message(
                _conversation, messageJson, extraContextJson, IntPtr.Zero);
            if (response == IntPtr.Zero)
            {
                throw new InvalidOperationException("litert_lm_conversation_send_message returned null.");
            }

            try
            {
                var ptr = LiteRtLmNative.litert_lm_json_response_get_string(response);
                return ptr == IntPtr.Zero ? string.Empty : (Marshal.PtrToStringUTF8(ptr) ?? string.Empty);
            }
            finally
            {
                LiteRtLmNative.litert_lm_json_response_delete(response);
            }
        }

        /// <summary>
        /// Sends a message JSON and invokes <paramref name="onChunkJson"/> for each streamed
        /// chunk JSON <b>on a background thread</b>. Blocks until the stream completes and
        /// returns the concatenated chunk payload.
        /// </summary>
        public string SendMessageStreamJson(
            string messageJson, Action<string> onChunkJson, string? extraContextJson = null)
        {
            if (messageJson == null) throw new ArgumentNullException(nameof(messageJson));
            if (onChunkJson == null) throw new ArgumentNullException(nameof(onChunkJson));

            using var context = new StreamCallbackBridge.Context(onChunkJson);
            var handle = StreamCallbackBridge.Pin(context);
            try
            {
                int status = LiteRtLmNative.litert_lm_conversation_send_message_stream(
                    _conversation, messageJson, extraContextJson, IntPtr.Zero,
                    StreamCallbackBridge.Callback, GCHandle.ToIntPtr(handle));
                if (status != 0)
                {
                    throw new InvalidOperationException(
                        $"litert_lm_conversation_send_message_stream failed with status {status}.");
                }
                return context.Wait();
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>Sends user text and returns the response text (blocking).</summary>
        public string SendMessage(string text)
        {
            string responseJson = SendMessageJson(LlmMessage.UserText(text));
            return LlmMessage.TryExtractText(responseJson, out string responseText)
                ? responseText
                : string.Empty;
        }

        /// <summary>
        /// Sends user text, invoking <paramref name="onTextChunk"/> with the text of each
        /// streamed chunk <b>on a background thread</b>. Blocks until the stream completes
        /// and returns the full response text.
        /// </summary>
        public string SendMessageStream(string text, Action<string> onTextChunk)
        {
            if (onTextChunk == null) throw new ArgumentNullException(nameof(onTextChunk));

            var builder = new System.Text.StringBuilder();
            SendMessageStreamJson(LlmMessage.UserText(text), chunkJson =>
            {
                if (LlmMessage.TryExtractText(chunkJson, out string chunkText) && chunkText.Length > 0)
                {
                    builder.Append(chunkText);
                    onTextChunk(chunkText);
                }
            });
            return builder.ToString();
        }

        /// <summary>Clones the conversation, including its history.</summary>
        public LlmConversation Clone()
        {
            var clone = LiteRtLmNative.litert_lm_conversation_clone(_conversation);
            if (clone == IntPtr.Zero)
            {
                throw new InvalidOperationException("litert_lm_conversation_clone returned null.");
            }
            return new LlmConversation(clone);
        }

        /// <summary>Requests cancellation of an in-progress generation.</summary>
        public void Cancel() => LiteRtLmNative.litert_lm_conversation_cancel_process(_conversation);

        public void Dispose()
        {
            if (_conversation != IntPtr.Zero)
            {
                LiteRtLmNative.litert_lm_conversation_delete(_conversation);
                _conversation = IntPtr.Zero;
            }
        }
    }
}
