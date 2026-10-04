using System.Runtime.CompilerServices;

namespace Ryzom.Core.Math3D;

public readonly struct Quaternionf : IEquatable<Quaternionf>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;
    public readonly float W;

    public static readonly Quaternionf Identity = new(0f, 0f, 0f, 1f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Quaternionf(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternionf operator *(Quaternionf a, Quaternionf b)
    {
        return new Quaternionf(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
            a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => X * X + Y * Y + Z * Z + W * W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Quaternionf Normalize()
    {
        float len = Length();
        return len > 1e-6f ? new Quaternionf(X / len, Y / len, Z / len, W / len) : Identity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternionf FromAxisAngle(Vector3f axis, float angleRadians)
    {
        float halfAngle = angleRadians * 0.5f;
        float s = MathF.Sin(halfAngle);
        var normAxis = axis.Normalize();
        return new Quaternionf(normAxis.X * s, normAxis.Y * s, normAxis.Z * s, MathF.Cos(halfAngle));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3f Rotate(Vector3f v)
    {
        Vector3f qVec = new(X, Y, Z);
        Vector3f uv = Vector3f.Cross(qVec, v);
        Vector3f uuv = Vector3f.Cross(qVec, uv);
        return v + (uv * W + uuv) * 2f;
    }

    public bool Equals(Quaternionf other) =>
        MathF.Abs(X - other.X) < 1e-5f &&
        MathF.Abs(Y - other.Y) < 1e-5f &&
        MathF.Abs(Z - other.Z) < 1e-5f &&
        MathF.Abs(W - other.W) < 1e-5f;

    public override bool Equals(object? obj) => obj is Quaternionf other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
}
