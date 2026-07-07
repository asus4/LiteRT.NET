using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;

namespace LiteRT.Unity
{
    public sealed class TextureToNativeTensorFloat32 : TextureToNativeTensor
    {
        public TextureToNativeTensorFloat32(Options options)
            : base(UnsafeUtility.SizeOf<float>(), options)
        { }
    }

    /// <summary>
    /// ComputeBuffer doesn't support UInt8, so the compute shader runs as Float32
    /// and the result is converted to UInt8 here in C#.
    /// </summary>
    public sealed class TextureToNativeTensorUInt8 : TextureToNativeTensor
    {
        const int kJOB_BATCH_SIZE = 64;
        private NativeArray<byte> tensorUInt8;

        public TextureToNativeTensorUInt8(Options options)
            : base(UnsafeUtility.SizeOf<uint>(), options)
        {
            int length = options.width * options.height * options.channels;
            tensorUInt8 = new NativeArray<byte>(length, Allocator.Persistent);
            Assert.AreEqual(tensor.Length / 4, tensorUInt8.Length, $"Length {tensor.Length} != {tensorUInt8.Length}");
        }

        public override void Dispose()
        {
            base.Dispose();
            tensorUInt8.Dispose();
        }

        public override NativeArray<byte> Transform(Texture input, in Matrix4x4 t)
        {
            NativeArray<byte> tensor = base.Transform(input, t);
            // Reinterpret (byte * 4) as float
            NativeSlice<float> tensorF32 = tensor.Slice().SliceConvert<float>();

            var job = new CastFloat32toUInt8Job()
            {
                input = tensorF32,
                output = tensorUInt8,
            };
            job.Schedule(tensorF32.Length, kJOB_BATCH_SIZE).Complete();
            return tensorUInt8;
        }

        [BurstCompile]
        struct CastFloat32toUInt8Job : IJobParallelFor
        {
            [ReadOnly]
            public NativeSlice<float> input;

            [WriteOnly]
            public NativeArray<byte> output;

            public void Execute(int index)
            {
                output[index] = (byte)(input[index] * 255f);
            }
        }
    }

    public sealed class TextureToNativeTensorInt32 : TextureToNativeTensor
    {
        const int kJOB_BATCH_SIZE = 64;
        private NativeArray<byte> tensorInt32;

        public TextureToNativeTensorInt32(Options options)
            : base(UnsafeUtility.SizeOf<uint>(), options)
        {
            int length = options.width * options.height * options.channels;
            int stride = UnsafeUtility.SizeOf<int>();
            tensorInt32 = new NativeArray<byte>(length * stride, Allocator.Persistent);
            Assert.AreEqual(tensor.Length, tensorInt32.Length, $"Length {tensor.Length} != {tensorInt32.Length}");
        }

        public override void Dispose()
        {
            base.Dispose();
            tensorInt32.Dispose();
        }

        public override NativeArray<byte> Transform(Texture input, in Matrix4x4 t)
        {
            NativeArray<byte> tensor = base.Transform(input, t);
            // Reinterpret (byte * 4) as float
            NativeSlice<float> sliceF32 = tensor.Slice().SliceConvert<float>();
            // Reinterpret (byte * 4) as int
            NativeSlice<int> sliceI32 = tensorInt32.Slice().SliceConvert<int>();

            var job = new CastFloat32toInt32Job()
            {
                input = sliceF32,
                output = sliceI32,
            };
            job.Schedule(sliceF32.Length, kJOB_BATCH_SIZE).Complete();
            return tensorInt32;
        }

        [BurstCompile]
        internal struct CastFloat32toInt32Job : IJobParallelFor
        {
            [ReadOnly]
            public NativeSlice<float> input;

            [WriteOnly]
            public NativeSlice<int> output;

            public void Execute(int index)
            {
                output[index] = (int)(input[index] * 255f);
            }
        }
    }
}
