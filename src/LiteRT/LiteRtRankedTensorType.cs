using System;
using LiteRT.Interop;

namespace LiteRT
{
    /// <summary>
    /// Element type and shape of a model tensor (managed mirror of the native
    /// <c>LiteRtRankedTensorType</c> struct).
    /// </summary>
    public readonly struct LiteRtRankedTensorType
    {
        /// <summary><c>LITERT_TENSOR_MAX_RANK</c> in the LiteRT C API.</summary>
        public const int MaxRank = 8;

        private readonly int[] _dimensions;

        public LiteRtElementType ElementType { get; }

        public int Rank => _dimensions?.Length ?? 0;

        /// <summary>Tensor dimensions; a dynamic dimension is reported as a negative value.</summary>
        public ReadOnlySpan<int> Dimensions => _dimensions;

        /// <summary>
        /// Product of all dimensions (1 for rank 0). Throws if any dimension is dynamic.
        /// </summary>
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

            // Leading fields of LiteRtRankedTensorType are layout-stable across compilers
            // (only the trailing stride bitfield packing differs):
            //   offset 0: int32  element_type
            //   offset 4: uint32 layout.rank
            //   offset 8: int32  layout.dimensions[LITERT_TENSOR_MAX_RANK]
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
