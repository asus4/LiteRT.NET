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
    /// <summary>
    /// Base class for vision task that takes a Texture as an input
    /// </summary>
    public abstract class BaseVisionTask : IDisposable
    {
        protected LiteRtEnvironment environment;
        protected LiteRtModelLoader modelLoader;
        protected LiteRtCompiledModel compiledModel;
        protected LiteRtSignature signature;
        protected LiteRtTensorBuffer[] inputBuffers;
        protected LiteRtTensorBuffer[] outputBuffers;
        protected int inputTensorIndex = 0;
        protected int width;
        protected int height;
        protected int channels;
        protected TextureToNativeTensor textureToTensor;

        private bool isDisposed = false;

        // Aspect scale applied to the last PreProcess input, used to map results back.
        private Vector2 aspectScale = Vector2.one;

        public AspectMode AspectMode { get; set; } = AspectMode.None;

        /// <summary>Becomes true once <see cref="InitializeAsync"/> has finished.</summary>
        public bool IsInitialized { get; private set; }

        protected static readonly ProfilerMarker preprocessPerfMarker = new($"{typeof(BaseVisionTask).Name}.Preprocess");
        protected static readonly ProfilerMarker runPerfMarker = new($"{typeof(BaseVisionTask).Name}.Session.Run");
        protected static readonly ProfilerMarker postprocessPerfMarker = new($"{typeof(BaseVisionTask).Name}.Postprocess");

        /// <summary>
        /// Load and compile the model, then allocate all input/output tensor buffers.
        /// </summary>
        /// <param name="modelPath">A model path: absolute, URL, or relative to StreamingAssets</param>
        /// <param name="accelerator">Hardware accelerator flags used to compile the model</param>
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

        /// <summary>
        /// Run the model with the input texture
        /// </summary>
        /// <param name="texture">A texture for model input</param>
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

        /// <summary>
        /// Pre process the input texture and set all input tensors for the model
        /// </summary>
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

        /// <summary>
        /// Get the output tensors and do post process in subclass
        /// </summary>
        protected abstract void PostProcess();

        /// <summary>
        /// Converts a normalized Rect in the model input space to the source texture space,
        /// undoing the AspectMode (letterbox/crop) scaling applied in PreProcess.
        /// The mapping is symmetric around the center, so it is valid for both
        /// top-left and bottom-left origins.
        /// </summary>
        /// <param name="rect">A normalized Rect in the model input space</param>
        /// <returns>A normalized Rect in the source texture space</returns>
        public Rect ConvertToTextureSpace(in Rect rect)
        {
            Vector2 scale = aspectScale;
            return new Rect(
                (rect.x - 0.5f) * scale.x + 0.5f,
                (rect.y - 0.5f) * scale.y + 0.5f,
                rect.width * scale.x,
                rect.height * scale.y);
        }

        /// <summary>
        /// Default implementation of InitializeInputsOutputs
        /// Override this in subclass if needed
        /// </summary>
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

        /// <summary>
        /// Create TextureToTensor for this model.
        /// Override this in subclass if needed
        /// </summary>
        /// <returns>A TextureToNativeTensor instance</returns>
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

        /// <summary>
        /// Resolve a serialized model path: URLs and absolute paths pass through,
        /// anything else is treated as relative to StreamingAssets.
        /// </summary>
        protected static string ResolvePath(string path)
        {
            if (path.Contains("://") || Path.IsPathRooted(path))
            {
                return path;
            }
            // On Android this produces a jar:file:// URL which LiteRtModelLoader
            // fetches via UnityWebRequest.
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
