using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LiteRT.Interop;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;

namespace LiteRT.Unity
{
    public abstract class BaseVisionTask : IDisposable
    {
        // Late-init: assigned by InitializeAsync; Run() guards on IsInitialized.
        protected LiteRtEnvironment environment = null!;
        protected LiteRtModelLoader modelLoader = null!;
        protected LiteRtCompiledModel compiledModel = null!;
        protected LiteRtSignature signature = null!;
        protected LiteRtTensorBuffer[] inputBuffers = null!;
        protected LiteRtTensorBuffer[] outputBuffers = null!;
        protected int inputTensorIndex = 0;
        protected int width;
        protected int height;
        protected int channels;
        protected TextureToNativeTensor textureToTensor = null!;

        private bool isDisposed = false;

        // Scale from the last PreProcess, used to map results back to texture space.
        private Vector2 aspectScale = Vector2.one;

        public AspectMode AspectMode { get; set; } = AspectMode.None;

        public bool IsInitialized { get; private set; }

        protected static readonly ProfilerMarker preprocessPerfMarker = new($"{typeof(BaseVisionTask).Name}.Preprocess");
        protected static readonly ProfilerMarker runPerfMarker = new($"{typeof(BaseVisionTask).Name}.Session.Run");
        protected static readonly ProfilerMarker postprocessPerfMarker = new($"{typeof(BaseVisionTask).Name}.Postprocess");

        /// <param name="modelPath">A model path: absolute, URL, or relative to StreamingAssets</param>
        protected async ValueTask InitializeAsync(
            string modelPath,
            LiteRtHwAccelerators accelerator = LiteRtHwAccelerators.Cpu,
            CancellationToken cancellationToken = default)
        {
            // A scene serialized without the accelerator field deserializes it as None.
            if (accelerator == LiteRtHwAccelerators.None)
            {
                accelerator = LiteRtHwAccelerators.Cpu;
            }

            environment = new LiteRtEnvironment(LiteRtHwAccelerators.Cpu | accelerator);
            modelLoader = new LiteRtModelLoader();
            LiteRtModel model = await modelLoader.LoadFromPathAsync(ResolvePath(modelPath), cancellationToken);
            compiledModel = new LiteRtCompiledModel(environment, model, LiteRtHwAccelerators.Cpu | accelerator);
            signature = model.GetSignature(0);

            LiteRtRankedTensorType inputType = signature.GetInputTensorType(inputTensorIndex);
            InitializeInputsOutputs(inputType);
            textureToTensor = CreateTextureToTensor(inputType);
            IsInitialized = true;
        }

        public virtual void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            // Buffers and compiled model must be released before the environment.
            DisposeAll(inputBuffers);
            DisposeAll(outputBuffers);
            compiledModel?.Dispose();
            modelLoader?.Dispose();
            textureToTensor?.Dispose();
            environment?.Dispose();
            isDisposed = true;
        }

        public virtual void Run(Texture texture)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(BaseVisionTask));
            }
            if (!IsInitialized)
            {
                throw new InvalidOperationException($"Call {nameof(InitializeAsync)} before {nameof(Run)}");
            }

            preprocessPerfMarker.Begin();
            PreProcess(texture);
            preprocessPerfMarker.End();

            runPerfMarker.Begin();
            compiledModel.Run(signature, inputBuffers, outputBuffers);
            runPerfMarker.End();

            postprocessPerfMarker.Begin();
            PostProcess();
            postprocessPerfMarker.End();
        }

        protected virtual void PreProcess(Texture texture)
        {
            float srcAspect = (float)texture.width / texture.height;
            float dstAspect = (float)width / height;
            aspectScale = TextureToNativeTensor.GetAspectScale(srcAspect, dstAspect, AspectMode);

            NativeArray<byte> input = textureToTensor.Transform(texture, AspectMode);
            LiteRtTensorBuffer inputBuffer = inputBuffers[inputTensorIndex];
            Assert.AreEqual(inputBuffer.PackedByteSize, input.Length,
                $"The transformed texture size ({input.Length} bytes) must match the input tensor size ({inputBuffer.PackedByteSize} bytes)");
            inputBuffer.Write(input.AsReadOnlySpan());
        }

        protected abstract void PostProcess();

        /// <summary>
        /// Maps a normalized Rect from model input space to source texture space, undoing the
        /// AspectMode scaling. Center-symmetric, so valid for both top-left and bottom-left origins.
        /// </summary>
        public Rect ConvertToTextureSpace(in Rect rect)
        {
            Vector2 scale = aspectScale;
            return new Rect(
                (rect.x - 0.5f) * scale.x + 0.5f,
                (rect.y - 0.5f) * scale.y + 0.5f,
                rect.width * scale.x,
                rect.height * scale.y);
        }

        protected virtual void InitializeInputsOutputs(in LiteRtRankedTensorType inputType)
        {
            ReadOnlySpan<int> inputShape = inputType.Dimensions;
            Assert.AreEqual(4, inputShape.Length);
            Assert.AreEqual(1, inputShape[0], $"The batch size of the model must be 1. But got {inputShape[0]}");
            height = inputShape[1];
            width = inputShape[2];
            channels = inputShape[3];

            inputBuffers = new LiteRtTensorBuffer[signature.InputCount];
            for (int i = 0; i < inputBuffers.Length; i++)
            {
                inputBuffers[i] = compiledModel.CreateInputBuffer(signature, i);
            }
            outputBuffers = new LiteRtTensorBuffer[signature.OutputCount];
            for (int i = 0; i < outputBuffers.Length; i++)
            {
                outputBuffers[i] = compiledModel.CreateOutputBuffer(signature, i);
            }
        }

        protected virtual TextureToNativeTensor CreateTextureToTensor(in LiteRtRankedTensorType inputType)
        {
            return TextureToNativeTensor.Create(new()
            {
                compute = null,
                kernel = 0,
                width = width,
                height = height,
                channels = channels,
                inputType = inputType.ElementType,
            });
        }

        protected static string ResolvePath(string path)
        {
            if (path.Contains("://") || Path.IsPathRooted(path))
            {
                return path;
            }
            // On Android this yields a jar:file:// URL, fetched via UnityWebRequest.
            return Path.Combine(Application.streamingAssetsPath, path);
        }

        private static void DisposeAll(LiteRtTensorBuffer[] buffers)
        {
            if (buffers == null)
            {
                return;
            }
            foreach (var buffer in buffers)
            {
                buffer?.Dispose();
            }
        }
    }
}
