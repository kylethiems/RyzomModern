using Ryzom.Core.Formats;
using Ryzom.Core.Math3D;

namespace Ryzom.Tests;

public class RayAABBCollisionTests
{
    [Fact]
    public void RayHitsBox_ReturnsTrue()
    {
        var box = new AABB(new Vector3f(-1f, -1f, -1f), new Vector3f(1f, 1f, 1f));
        var ray = new Ray3D(new Vector3f(0f, 0f, -5f), Vector3f.UnitZ);

        bool hit = box.IntersectsRay(ray, out float tMin, out float tMax);

        Assert.True(hit);
        Assert.True(MathF.Abs(tMin - 4f) < 1e-4f);
        Assert.True(MathF.Abs(tMax - 6f) < 1e-4f);
    }

    [Fact]
    public void RayMissesBox_ReturnsFalse()
    {
        var box = new AABB(new Vector3f(-1f, -1f, -1f), new Vector3f(1f, 1f, 1f));
        var ray = new Ray3D(new Vector3f(10f, 0f, -5f), Vector3f.UnitZ); // Offset on X

        bool hit = box.IntersectsRay(ray, out _, out _);

        Assert.False(hit);
    }

    [Fact]
    public void VoxelDecimation_WeldsDuplicateVertices()
    {
        var mesh = new NeLMesh();
        // Add 4 vertices where 2 are almost identical (within 0.1mm)
        mesh.Vertices.Add(new Vertex3D(new Vector3f(0f, 0f, 0f), Vector3f.UnitY, 0f, 0f));
        mesh.Vertices.Add(new Vertex3D(new Vector3f(1f, 0f, 0f), Vector3f.UnitY, 1f, 0f));
        mesh.Vertices.Add(new Vertex3D(new Vector3f(0f, 1f, 0f), Vector3f.UnitY, 0f, 1f));
        mesh.Vertices.Add(new Vertex3D(new Vector3f(0.00001f, 0f, 0f), Vector3f.UnitY, 0f, 0f)); // Duplicate of vertex 0

        mesh.Indices.AddRange(new ushort[] { 0, 1, 2, 3, 1, 2 });

        int removed = mesh.DecimateAndClean(voxelTolerance: 0.001f);

        Assert.Equal(1, removed);
        Assert.Equal(3, mesh.Vertices.Count);
    }
}
