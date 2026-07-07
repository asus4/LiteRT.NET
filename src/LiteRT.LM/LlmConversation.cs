using System;
using System.Runtime.InteropServices;
using LiteRT.LM.Interop;

namespace LiteRT.LM
{
    /// <summary>Multi-turn conversation; history lives in the KV cache. The <c>*Json</c> methods are
    /// lossless (tools, channels, multimodal); <see cref="SendMessage"/>/<see cref="SendMessageStream"/>
    /// are text-only. Streaming callbacks fire on a native background thread.</summary>
    public sealed class LlmConversation : IDisposable
    {
        private IntPtr _conversation;

        internal LlmConversation(IntPtr conversation) => _conversation = conversation;

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

        public string SendMessage(string text)
        {
            string responseJson = SendMessageJson(LlmMessage.UserText(text));
            return LlmMessage.TryExtractText(responseJson, out string responseText)
                ? responseText
                : string.Empty;
        }

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

        public LlmConversation Clone()
        {
            var clone = LiteRtLmNative.litert_lm_conversation_clone(_conversation);
            if (clone == IntPtr.Zero)
            {
                throw new InvalidOperationException("litert_lm_conversation_clone returned null.");
            }
            return new LlmConversation(clone);
        }

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
