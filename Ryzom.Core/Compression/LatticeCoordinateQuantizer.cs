using System.Runtime.CompilerServices;
using Ryzom.Core.Math3D;

namespace Ryzom.Core.Compression;

/// <summary>
/// High-density 3D coordinate quantizer using fixed-point lattice compression.
/// Compresses full 12-byte float32 coordinates into a 4-byte packed integer or 6-byte delta.
/// </summary>
public static class LatticeCoordinateQuantizer
{
    // Quantize 3D point inside a 1024-meter cell with 1-millimeter precision
    public const float CELL_RESOLUTION = 0.001f; // 1 mm

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint PackPoint10Bit(Vector3f localPoint, float cellExtent)
    {
        // 10 bits per axis = 0..1023 normalized resolution
        uint qx = (uint)Math.Clamp((localPoint.X / cellExtent) * 1023f, 0f, 1023f);
        uint qy = (uint)Math.Clamp((localPoint.Y / cellExtent) * 1023f, 0f, 1023f);
        uint qz = (uint)Math.Clamp((localPoint.Z / cellExtent) * 1023f, 0f, 1023f);

        return (qx & 0x3FF) | ((qy & 0x3FF) << 10) | ((qz & 0x3FF) << 20);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f UnpackPoint10Bit(uint packed, float cellExtent)
    {
        float qx = (packed & 0x3FF) / 1023f;
        float qy = ((packed >> 10) & 0x3FF) / 1023f;
        float qz = ((packed >> 20) & 0x3FF) / 1023f;

        return new Vector3f(qx * cellExtent, qy * cellExtent, qz * cellExtent);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PackDelta16Bit(Vector3f delta, Span<byte> destination)
    {
        if (destination.Length < 6)
            throw new ArgumentException("Destination span must be at least 6 bytes.", nameof(destination));

        // Scale by 100 (centimeter precision across +/- 327 meters)
        short dx = (short)Math.Clamp(delta.X * 100f, short.MinValue, short.MaxValue);
        short dy = (short)Math.Clamp(delta.Y * 100f, short.MinValue, short.MaxValue);
        short dz = (short)Math.Clamp(delta.Z * 100f, short.MinValue, short.MaxValue);

        destination[0] = (byte)(dx & 0xFF);
        destination[1] = (byte)((dx >> 8) & 0xFF);
        destination[2] = (byte)(dy & 0xFF);
        destination[3] = (byte)((dy >> 8) & 0xFF);
        destination[4] = (byte)(dz & 0xFF);
        destination[5] = (byte)((dz >> 8) & 0xFF);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f UnpackDelta16Bit(ReadOnlySpan<byte> source)
    {
        if (source.Length < 6)
            throw new ArgumentException("Source span must be at least 6 bytes.", nameof(source));

        short dx = (short)(source[0] | (source[1] << 8));
        short dy = (short)(source[2] | (source[3] << 8));
        short dz = (short)(source[4] | (source[5] << 8));

        return new Vector3f(dx / 100f, dy / 100f, dz / 100f);
    }
}
