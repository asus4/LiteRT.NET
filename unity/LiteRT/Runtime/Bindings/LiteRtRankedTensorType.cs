using System;
using LiteRT.Interop;

namespace LiteRT
{
    /// <summary>Managed mirror of the native <c>LiteRtRankedTensorType</c>.</summary>
    public readonly struct LiteRtRankedTensorType
    {
        /// <summary><c>LITERT_TENSOR_MAX_RANK</c>.</summary>
        public const int MaxRank = 8;

        private readonly int[] _dimensions;

        public LiteRtElementType ElementType { get; }

        public int Rank => _dimensions?.Length ?? 0;

        /// <summary>A dynamic dimension is reported as a negative value.</summary>
        public ReadOnlySpan<int> Dimensions => _dimensions;

        /// <summary>Throws if any dimension is dynamic.</summary>
        public int ElementCount
        {
            get
            {
                int count = 1;
                for (int i = 0; i < Rank; i++)
                {
                    int dim = _dimensions[i];
                    if (dim < 0)
                    {
                        throw new InvalidOperationException(
                            $"Dimension {i} is dynamic ({dim}); the element count is unknown until the tensor is resolved.");
                    }
                    count = checked(count * dim);
                }
                return count;
            }
        }

        private LiteRtRankedTensorType(LiteRtElementType elementType, int[] dimensions)
        {
            ElementType = elementType;
            _dimensions = dimensions;
        }

        public override string ToString() =>
            $"{ElementType}[{string.Join(", ", _dimensions ?? Array.Empty<int>())}]";

        internal static unsafe LiteRtRankedTensorType FromTensor(IntPtr tensor)
        {
            byte* blob = stackalloc byte[LiteRtNative.RankedTensorTypeSize];
            LiteRtException.ThrowIfError(
                LiteRtNative.LiteRtGetRankedTensorType(tensor, blob),
                nameof(LiteRtNative.LiteRtGetRankedTensorType));

            // Leading fields are layout-stable across compilers (only trailing stride bitfields differ):
            // offset 0 int32 element_type, offset 4 uint32 rank, offset 8 int32 dims[MaxRank].
            var elementType = (LiteRtElementType)(*(int*)blob);
            uint rank = *(uint*)(blob + 4);
            if (rank > MaxRank)
            {
                throw new InvalidOperationException(
                    $"Parsed tensor rank {rank} exceeds LITERT_TENSOR_MAX_RANK ({MaxRank}). " +
                    "The native LiteRtRankedTensorType layout no longer matches the assumed field offsets.");
            }

            var dimensions = new int[rank];
            for (int i = 0; i < rank; i++)
            {
                dimensions[i] = *(int*)(blob + 8 + i * sizeof(int));
            }
            return new LiteRtRankedTensorType(elementType, dimensions);
        }
    }
}
