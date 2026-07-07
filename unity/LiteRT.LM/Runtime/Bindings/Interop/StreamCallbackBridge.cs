using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LiteRT.LM.Interop
{
    // IL2CPP can't marshal instance delegates to native function pointers, so all streams share
    // one static callback and pass per-call state as a GCHandle via callback_data.
    // Chunks arrive on a native background thread.
    internal static class StreamCallbackBridge
    {
        internal sealed class Context : IDisposable
        {
            private readonly StringBuilder _builder = new StringBuilder();
            private readonly ManualResetEventSlim _done = new ManualResetEventSlim(false);
            private readonly Action<string> _onChunk;
            private string? _error;

            internal Context(Action<string> onChunk) => _onChunk = onChunk;

            internal void Append(string chunk)
            {
                _builder.Append(chunk);
                _onChunk(chunk);
            }

            internal void Complete() => _done.Set();

            internal void SetError(string error) => _error = error;

            // The "Max number of tokens" error is a normal length-limited stop, not a failure.
            internal string Wait()
            {
                _done.Wait();
                if (_error != null && _error.IndexOf("Max number of tokens", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException($"LiteRT-LM streaming error: {_error}");
                }
                return _builder.ToString();
            }

            public void Dispose() => _done.Dispose();
        }

        internal static readonly LiteRtLmStreamCallback Callback = OnChunk;

        internal static GCHandle Pin(Context context) => GCHandle.Alloc(context);

        [MonoPInvokeCallback(typeof(LiteRtLmStreamCallback))]
        private static void OnChunk(IntPtr callbackData, string? chunk, bool isFinal, string? errorMsg)
        {
            var handle = GCHandle.FromIntPtr(callbackData);
            if (!(handle.Target is Context context))
            {
                return;
            }
            if (!string.IsNullOrEmpty(errorMsg))
            {
                context.SetError(errorMsg!);
            }
            if (!string.IsNullOrEmpty(chunk))
            {
                context.Append(chunk!);
            }
            if (isFinal)
            {
                context.Complete();
            }
        }
    }
}
