using System;
using System.Runtime.InteropServices;
using LiteRT.Interop;

namespace LiteRT
{
    public sealed class LiteRtException : Exception
    {
        public LiteRtStatus Status { get; }

        internal LiteRtException(LiteRtStatus status, string operation)
            : base($"{operation} failed: {status} ({DescribeStatus(status)})")
        {
            Status = status;
        }

        internal static void ThrowIfError(LiteRtStatus status, string operation)
        {
            if (status != LiteRtStatus.Ok)
            {
                throw new LiteRtException(status, operation);
            }
        }

        private static string DescribeStatus(LiteRtStatus status)
        {
            var ptr = LiteRtNative.LiteRtGetStatusString(status);
            return ptr == IntPtr.Zero ? "unknown" : (Marshal.PtrToStringUTF8(ptr) ?? "unknown");
        }
    }
}
