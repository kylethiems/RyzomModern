using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryzom.Core.Math3D;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Vector3f : IEquatable<Vector3f>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    public static readonly Vector3f Zero = new(0f, 0f, 0f);
    public static readonly Vector3f One = new(1f, 1f, 1f);
    public static readonly Vector3f UnitX = new(1f, 0f, 0f);
    public static readonly Vector3f UnitY = new(0f, 1f, 0f);
    public static readonly Vector3f UnitZ = new(0f, 0f, 1f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3f(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator +(Vector3f a, Vector3f b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator -(Vector3f a, Vector3f b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator *(Vector3f v, float scalar) => new(v.X * scalar, v.Y * scalar, v.Z * scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator /(Vector3f v, float scalar) => new(v.X / scalar, v.Y / scalar, v.Z / scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => X * X + Y * Y + Z * Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3f Normalize()
    {
        float len = Length();
        return len > 1e-6f ? this / len : Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector3f a, Vector3f b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f Cross(Vector3f a, Vector3f b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector3f a, Vector3f b) => (a - b).Length();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector3f a, Vector3f b) => (a - b).LengthSquared();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f Lerp(Vector3f a, Vector3f b, float t) => a + (b - a) * t;

    public bool Equals(Vector3f other) =>
        MathF.Abs(X - other.X) < 1e-5f &&
        MathF.Abs(Y - other.Y) < 1e-5f &&
        MathF.Abs(Z - other.Z) < 1e-5f;

    public override bool Equals(object? obj) => obj is Vector3f other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"Vector3f({X:F3}, {Y:F3}, {Z:F3})";
}
