using Ryzom.Core.Math3D;

namespace Ryzom.Tests;

public class VectorMathTests
{
    [Fact]
    public void VectorAdditionAndLength_BehavesCorrectly()
    {
        var v1 = new Vector3f(3f, 0f, 0f);
        var v2 = new Vector3f(0f, 4f, 0f);
        var sum = v1 + v2;

        Assert.Equal(3f, sum.X);
        Assert.Equal(4f, sum.Y);
        Assert.Equal(0f, sum.Z);
        Assert.Equal(5f, sum.Length());
    }

    [Fact]
    public void DotAndCrossProducts_SatisfyOrthogonality()
    {
        var unitX = Vector3f.UnitX;
        var unitY = Vector3f.UnitY;

        Assert.Equal(0f, Vector3f.Dot(unitX, unitY));

        var cross = Vector3f.Cross(unitX, unitY);
        Assert.Equal(Vector3f.UnitZ, cross);
    }

    [Fact]
    public void Lerp_InterpolatesAccurately()
    {
        var a = new Vector3f(0f, 0f, 0f);
        var b = new Vector3f(10f, 20f, 30f);

        var mid = Vector3f.Lerp(a, b, 0.5f);
        Assert.Equal(new Vector3f(5f, 10f, 15f), mid);
    }

    [Fact]
    public void Quaternion_RotatesVector90DegreesAroundZ()
    {
        var rot90Z = Quaternionf.FromAxisAngle(Vector3f.UnitZ, MathF.PI * 0.5f);
        var v = Vector3f.UnitX;

        var rotated = rot90Z.Rotate(v);

        Assert.True(MathF.Abs(rotated.X - 0f) < 1e-4f);
        Assert.True(MathF.Abs(rotated.Y - 1f) < 1e-4f);
        Assert.True(MathF.Abs(rotated.Z - 0f) < 1e-4f);
    }
}
