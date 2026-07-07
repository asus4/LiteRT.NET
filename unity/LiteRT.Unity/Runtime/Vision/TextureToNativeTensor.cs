using System;
using LiteRT.Interop;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace LiteRT.Unity
{
    /// <summary>
    /// Converts Texture to Tensor with arbitrary matrix transformation
    /// then return it as a NativeArray<byte> (NHWC layout)
    /// </summary>
    public abstract class TextureToNativeTensor : IDisposable
    {
        [Serializable]
        public class Options
        {
            public ComputeShader compute = null;
            public int kernel = 0;
            public int width = 0;
            public int height = 0;
            public int channels = 0;
            public LiteRtElementType inputType = LiteRtElementType.Float32;
        }

        protected static readonly Lazy<ComputeShader> DefaultComputeShaderFloat32 = new(()
            => Resources.Load<ComputeShader>("com.koki-ibukuro.litert.unity/TextureToNativeTensorFloat32"));

        public static ComputeShader CloneDefaultComputeShaderFloat32()
        {
            return UnityEngine.Object.Instantiate(DefaultComputeShaderFloat32.Value);
        }

        private static readonly int _InputTex = Shader.PropertyToID("_InputTex");
        private static readonly int _OutputTensor = Shader.PropertyToID("_OutputTensor");
        private static readonly int _OutputSize = Shader.PropertyToID("_OutputSize");
        private static readonly int _TransformMatrix = Shader.PropertyToID("_TransformMatrix");

        private static readonly Matrix4x4 PopMatrix = Matrix4x4.Translate(new Vector3(0.5f, 0.5f, 0));
        private static readonly Matrix4x4 PushMatrix = Matrix4x4.Translate(new Vector3(-0.5f, -0.5f, 0));

        public readonly ComputeShader compute;
        public readonly bool hasCustomCompute;
        public readonly int kernel;
        public readonly int width;
        public readonly int height;
        public readonly int channels;

        private bool disposed = false;
        private readonly GraphicsBuffer tensorBuffer;
        protected NativeArray<byte> tensor;

        protected TextureToNativeTensor(int stride, Options options)
        {
            bool isSupported = SystemInfo.supportsAsyncGPUReadback && SystemInfo.supportsComputeShaders;
            if (!isSupported)
            {
                // Note: Async GPU Readback is supported on most platforms
                //       including OpenGL ES 3.0 since Unity 2021 LTS
                throw new NotSupportedException("AsyncGPUReadback and ComputeShader are required to use TextureToNativeTensor");
            }

            hasCustomCompute = options.compute != null;
            compute = hasCustomCompute
                ? options.compute
                : CloneDefaultComputeShaderFloat32();
            kernel = options.kernel;
            width = options.width;
            height = options.height;
            channels = options.channels;

            Assert.IsTrue(kernel >= 0, $"Kernel must be set");
            Assert.IsTrue(width > 0, $"Width must be greater than 0");
            Assert.IsTrue(height > 0, $"Height must be greater than 0");
            Assert.IsTrue(channels > 0 && channels <= 4, $"Channels must be 1 to 4");

            int length = width * height * channels;
            tensorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, length, stride);
            tensor = new NativeArray<byte>(length * stride, Allocator.Persistent);

            compute.SetInts(_OutputSize, width, height);
            compute.SetBuffer(kernel, _OutputTensor, tensorBuffer);
        }

        ~TextureToNativeTensor()
        {
            Dispose(false);
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (!hasCustomCompute)
                {
                    UnityEngine.Object.Destroy(compute);
                }
                tensor.Dispose();
                tensorBuffer.Dispose();
            }
            disposed = true;
        }

        public virtual NativeArray<byte> Transform(Texture input, in Matrix4x4 t)
        {
            compute.SetTexture(kernel, _InputTex, input, 0);
            compute.SetMatrix(_TransformMatrix, t);
            compute.Dispatch(kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);
            var request = AsyncGPUReadback.RequestIntoNativeArray(ref tensor, tensorBuffer);
            request.WaitForCompletion();
            return tensor;
        }

        public NativeArray<byte> Transform(Texture input, AspectMode aspectMode)
        {
            return Transform(input, GetAspectScaledMatrix(input, aspectMode));
        }

        public Matrix4x4 GetAspectScaledMatrix(Texture input, AspectMode aspectMode)
        {
            float srcAspect = (float)input.width / input.height;
            float dstAspect = (float)width / height;
            Vector2 scale = GetAspectScale(srcAspect, dstAspect, aspectMode);
            return PopMatrix * Matrix4x4.Scale(new Vector3(scale.x, scale.y, 1)) * PushMatrix;
        }

        public static Vector2 GetAspectScale(float srcAspect, float dstAspect, AspectMode mode)
        {
            bool isSrcWider = srcAspect > dstAspect;
            return (mode, isSrcWider) switch
            {
                (AspectMode.None, _) => new Vector2(1, 1),
                (AspectMode.Fit, true) => new Vector2(1, srcAspect / dstAspect),
                (AspectMode.Fit, false) => new Vector2(dstAspect / srcAspect, 1),
                (AspectMode.Fill, true) => new Vector2(dstAspect / srcAspect, 1),
                (AspectMode.Fill, false) => new Vector2(1, srcAspect / dstAspect),
                _ => throw new Exception("Unknown aspect mode"),
            };
        }

        /// <summary>
        /// Find the appropriate TextureToNativeTensor class for the given input type
        /// </summary>
        public static TextureToNativeTensor Create(Options options)
        {
            return options.inputType switch
            {
                LiteRtElementType.Float32 => new TextureToNativeTensorFloat32(options),
                LiteRtElementType.UInt8 => new TextureToNativeTensorUInt8(options),
                LiteRtElementType.Int32 => new TextureToNativeTensorInt32(options),
                _ => throw new NotImplementedException(
                    $"input type {options.inputType} is not implemented yet. Create our own TextureToNativeTensor class and override it."),
            };
        }
    }
}
