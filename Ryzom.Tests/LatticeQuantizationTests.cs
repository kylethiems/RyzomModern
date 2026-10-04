using Ryzom.Core.Compression;
using Ryzom.Core.Math3D;

namespace Ryzom.Tests;

public class LatticeQuantizationTests
{
    [Fact]
    public void PackUnpack10Bit_PreservesBoundedPosition()
    {
        float cellExtent = 256f;
        var original = new Vector3f(128.5f, 64.2f, 200.1f);

        uint packed = LatticeCoordinateQuantizer.PackPoint10Bit(original, cellExtent);
        var unpacked = LatticeCoordinateQuantizer.UnpackPoint10Bit(packed, cellExtent);

        // 10 bits over 256m gives ~0.25m maximum resolution step
        Assert.True(MathF.Abs(original.X - unpacked.X) < 0.3f);
        Assert.True(MathF.Abs(original.Y - unpacked.Y) < 0.3f);
        Assert.True(MathF.Abs(original.Z - unpacked.Z) < 0.3f);
    }

    [Fact]
    public void PackUnpackDelta16Bit_PreservesCentimeterPrecision()
    {
        var delta = new Vector3f(12.34f, -45.67f, 8.91f);
        Span<byte> buffer = stackalloc byte[6];

        LatticeCoordinateQuantizer.PackDelta16Bit(delta, buffer);
        var unpacked = LatticeCoordinateQuantizer.UnpackDelta16Bit(buffer);

        Assert.True(MathF.Abs(delta.X - unpacked.X) < 0.015f);
        Assert.True(MathF.Abs(delta.Y - unpacked.Y) < 0.015f);
        Assert.True(MathF.Abs(delta.Z - unpacked.Z) < 0.015f);
    }
}
