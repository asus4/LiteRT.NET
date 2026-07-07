using System;
using System.Collections.Generic;
using LiteRT.Interop;

namespace LiteRT
{
    public sealed unsafe class LiteRtCompiledModel : IDisposable
    {
        private readonly LiteRtEnvironment _environment;
        private IntPtr _handle;

        public LiteRtCompiledModel(LiteRtEnvironment environment, LiteRtModel model,
            LiteRtHwAccelerators accelerators = LiteRtHwAccelerators.Cpu)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            if (model == null) throw new ArgumentNullException(nameof(model));

            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtCreateOptions(out var options),
                nameof(LiteRtNative.LiteRtCreateOptions));
            try
            {
                LiteRtException.ThrowIfError(
                    LiteRtNative.LiteRtSetOptionsHardwareAccelerators(options, (int)accelerators),
                    nameof(LiteRtNative.LiteRtSetOptionsHardwareAccelerators));
                LiteRtException.ThrowIfError(
                    LiteRtNative.LiteRtCreateCompiledModel(environment.Handle, model.Handle, options, out _handle),
                    nameof(LiteRtNative.LiteRtCreateCompiledModel));
            }
            finally
            {
                LiteRtNative.LiteRtDestroyOptions(options);
            }
        }

        public LiteRtTensorBuffer CreateInputBuffer(LiteRtSignature signature, int inputIndex)
        {
            var tensor = signature.GetInputTensor(inputIndex);
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtGetCompiledModelInputBufferRequirements(
                    _handle, (UIntPtr)signature.Index, (UIntPtr)inputIndex, out var req),
                nameof(LiteRtNative.LiteRtGetCompiledModelInputBufferRequirements));
            return CreateBuffer(tensor, req);
        }

        public LiteRtTensorBuffer CreateOutputBuffer(LiteRtSignature signature, int outputIndex)
        {
            var tensor = signature.GetOutputTensor(outputIndex);
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtGetCompiledModelOutputBufferRequirements(
                    _handle, (UIntPtr)signature.Index, (UIntPtr)outputIndex, out var req),
                nameof(LiteRtNative.LiteRtGetCompiledModelOutputBufferRequirements));
            return CreateBuffer(tensor, req);
        }

        private LiteRtTensorBuffer CreateBuffer(IntPtr tensor, IntPtr requirements)
        {
            byte* blob = stackalloc byte[LiteRtNative.RankedTensorTypeSize];
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtGetRankedTensorType(tensor, blob),
                nameof(LiteRtNative.LiteRtGetRankedTensorType));

            // element_type is the first 4-byte field of LiteRtRankedTensorType.
            var elementType = (LiteRtElementType)(*(int*)blob);

            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtCreateManagedTensorBufferFromRequirements(
                    _environment.Handle, blob, requirements, out var buffer),
                nameof(LiteRtNative.LiteRtCreateManagedTensorBufferFromRequirements));
            return new LiteRtTensorBuffer(buffer, elementType);
        }

        public void Run(LiteRtSignature signature,
            IReadOnlyList<LiteRtTensorBuffer> inputs, IReadOnlyList<LiteRtTensorBuffer> outputs)
        {
            var inputHandles = ToHandles(inputs);
            var outputHandles = ToHandles(outputs);
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtRunCompiledModel(
                    _handle, (UIntPtr)signature.Index,
                    (UIntPtr)inputHandles.Length, inputHandles,
                    (UIntPtr)outputHandles.Length, outputHandles),
                nameof(LiteRtNative.LiteRtRunCompiledModel));
        }

        private static IntPtr[] ToHandles(IReadOnlyList<LiteRtTensorBuffer> buffers)
        {
            var handles = new IntPtr[buffers.Count];
            for (int i = 0; i < buffers.Count; i++)
            {
                handles[i] = buffers[i].Handle;
            }
            return handles;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                LiteRtNative.LiteRtDestroyCompiledModel(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
