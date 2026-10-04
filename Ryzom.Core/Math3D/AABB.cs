using System.Runtime.CompilerServices;

namespace Ryzom.Core.Math3D;

public readonly struct Ray3D
{
    public readonly Vector3f Origin;
    public readonly Vector3f Direction; // Must be normalized

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ray3D(Vector3f origin, Vector3f direction)
    {
        Origin = origin;
        Direction = direction.Normalize();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3f GetPoint(float distance) => Origin + Direction * distance;
}

public readonly struct AABB
{
    public readonly Vector3f Min;
    public readonly Vector3f Max;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AABB(Vector3f min, Vector3f max)
    {
        Min = new Vector3f(MathF.Min(min.X, max.X), MathF.Min(min.Y, max.Y), MathF.Min(min.Z, max.Z));
        Max = new Vector3f(MathF.Max(min.X, max.X), MathF.Max(min.Y, max.Y), MathF.Max(min.Z, max.Z));
    }

    public Vector3f Center => (Min + Max) * 0.5f;
    public Vector3f Extents => (Max - Min) * 0.5f;
    public Vector3f Size => Max - Min;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Vector3f p)
    {
        return p.X >= Min.X && p.X <= Max.X &&
               p.Y >= Min.Y && p.Y <= Max.Y &&
               p.Z >= Min.Z && p.Z <= Max.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(AABB other)
    {
        return Min.X <= other.Max.X && Max.X >= other.Min.X &&
               Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
               Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
    }

    /// <summary>
    /// Fast Ray-AABB intersection test using the slab method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IntersectsRay(Ray3D ray, out float tMin, out float tMax)
    {
        tMin = 0f;
        tMax = float.MaxValue;

        // X slab
        if (MathF.Abs(ray.Direction.X) < 1e-7f)
        {
            if (ray.Origin.X < Min.X || ray.Origin.X > Max.X) return false;
        }
        else
        {
            float ood = 1.0f / ray.Direction.X;
            float t1 = (Min.X - ray.Origin.X) * ood;
            float t2 = (Max.X - ray.Origin.X) * ood;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        // Y slab
        if (MathF.Abs(ray.Direction.Y) < 1e-7f)
        {
            if (ray.Origin.Y < Min.Y || ray.Origin.Y > Max.Y) return false;
        }
        else
        {
            float ood = 1.0f / ray.Direction.Y;
            float t1 = (Min.Y - ray.Origin.Y) * ood;
            float t2 = (Max.Y - ray.Origin.Y) * ood;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        // Z slab
        if (MathF.Abs(ray.Direction.Z) < 1e-7f)
        {
            if (ray.Origin.Z < Min.Z || ray.Origin.Z > Max.Z) return false;
        }
        else
        {
            float ood = 1.0f / ray.Direction.Z;
            float t1 = (Min.Z - ray.Origin.Z) * ood;
            float t2 = (Max.Z - ray.Origin.Z) * ood;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        return true;
    }
}
