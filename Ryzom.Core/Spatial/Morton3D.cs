using System.Runtime.CompilerServices;

namespace Ryzom.Core.Spatial;

/// <summary>
/// High-performance 64-bit Morton Z-Order spatial hashing for 3D coordinates.
/// Maps 3D space into a 1D linear array preserving spatial locality (cache line friendly).
/// </summary>
public static class Morton3D
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Encode(uint x, uint y, uint z)
    {
        return SplitBy3(x) | (SplitBy3(y) << 1) | (SplitBy3(z) << 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (uint X, uint Y, uint Z) Decode(ulong code)
    {
        return (CompactBy3(code), CompactBy3(code >> 1), CompactBy3(code >> 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong QuantizeAndEncode(float x, float y, float z, float minBound, float cellSize)
    {
        uint ux = (uint)Math.Clamp((x - minBound) / cellSize, 0f, 2097151f);
        uint uy = (uint)Math.Clamp((y - minBound) / cellSize, 0f, 2097151f);
        uint uz = (uint)Math.Clamp((z - minBound) / cellSize, 0f, 2097151f);
        return Encode(ux, uy, uz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong SplitBy3(uint a)
    {
        ulong x = a & 0x1fffff; // 21 bits
        x = (x | (x << 32)) & 0x1f00000000ffff;
        x = (x | (x << 16)) & 0x1f0000ff0000ff;
        x = (x | (x << 8))  & 0x100f00f00f00f00f;
        x = (x | (x << 4))  & 0x10c30c30c30c30c3;
        x = (x | (x << 2))  & 0x1249249249249249;
        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CompactBy3(ulong x)
    {
        x &= 0x1249249249249249;
        x = (x ^ (x >> 2))  & 0x10c30c30c30c30c3;
        x = (x ^ (x >> 4))  & 0x100f00f00f00f00f;
        x = (x ^ (x >> 8))  & 0x1f0000ff0000ff;
        x = (x ^ (x >> 16)) & 0x1f00000000ffff;
        x = (x ^ (x >> 32)) & 0x1fffff;
        return (uint)x;
    }
}
