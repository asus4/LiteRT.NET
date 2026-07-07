using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LiteRT.LM.Interop
{
    /// <summary>
    /// AOT-safe bridge for <see cref="LiteRtLmStreamCallback"/> reverse P/Invoke.
    ///
    /// IL2CPP cannot marshal instance-method (closure) delegates to native function
    /// pointers, so all streaming calls share one static callback and route per-call
    /// state through <c>callback_data</c> as a pinned <see cref="GCHandle"/> to a
    /// <see cref="Context"/>. Chunks arrive on a native background thread; the context
    /// forwards them to the caller's <c>onChunk</c> on that thread and signals
    /// completion via <see cref="Context.Wait"/>.
    /// </summary>
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

            /// <summary>
            /// Blocks until the final chunk arrives, then returns the concatenated text.
            /// Throws on stream errors, except the engine's "Max number of tokens"
            /// signal which is a normal length-limited stop.
            /// </summary>
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

        /// <summary>The shared static delegate instance, rooted for the process lifetime.</summary>
        internal static readonly LiteRtLmStreamCallback Callback = OnChunk;

        /// <summary>Allocates the GCHandle passed to native as <c>callback_data</c>.</summary>
        internal static GCHandle Pin(Context context) => GCHandle.Alloc(context);

#if ENABLE_IL2CPP
        [AOT.MonoPInvokeCallback(typeof(LiteRtLmStreamCallback))]
#endif
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
