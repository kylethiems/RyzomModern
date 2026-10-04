using Ryzom.Core.Math3D;

namespace Ryzom.Core.Formats;

public struct Vertex3D
{
    public Vector3f Position;
    public Vector3f Normal;
    public float U;
    public float V;

    public Vertex3D(Vector3f pos, Vector3f normal, float u, float v)
    {
        Position = pos;
        Normal = normal;
        U = u;
        V = v;
    }
}

public class NeLMesh
{
    public string Name { get; set; } = string.Empty;
    public List<Vertex3D> Vertices { get; set; } = new();
    public List<ushort> Indices { get; set; } = new();
    public AABB BoundingBox { get; private set; }

    public void RecalculateBounds()
    {
        if (Vertices.Count == 0)
        {
            BoundingBox = new AABB(Vector3f.Zero, Vector3f.Zero);
            return;
        }

        Vector3f min = Vertices[0].Position;
        Vector3f max = Vertices[0].Position;

        foreach (var v in Vertices)
        {
            min = new Vector3f(MathF.Min(min.X, v.Position.X), MathF.Min(min.Y, v.Position.Y), MathF.Min(min.Z, v.Position.Z));
            max = new Vector3f(MathF.Max(max.X, v.Position.X), MathF.Max(max.Y, v.Position.Y), MathF.Max(max.Z, v.Position.Z));
        }

        BoundingBox = new AABB(min, max);
    }

    /// <summary>
    /// Applies Voxel Grid LiDAR cleanup: welds duplicate or microscopic vertices
    /// to their voxel centroid and removes degenerate zero-area triangles.
    /// </summary>
    public int DecimateAndClean(float voxelTolerance = 0.001f) // 1mm default
    {
        int initialCount = Vertices.Count;
        var uniqueVertices = new List<Vertex3D>();
        var remap = new Dictionary<long, ushort>();
        var newIndices = new List<ushort>();

        for (int i = 0; i < Vertices.Count; i++)
        {
            var v = Vertices[i];
            long key = HashVertexVoxel(v.Position, voxelTolerance);

            if (!remap.TryGetValue(key, out ushort newIdx))
            {
                newIdx = (ushort)uniqueVertices.Count;
                uniqueVertices.Add(v);
                remap[key] = newIdx;
            }
        }

        // Remap triangles and filter out degenerate faces (where two vertices are identical)
        for (int i = 0; i < Indices.Count; i += 3)
        {
            if (i + 2 >= Indices.Count) break;

            long k0 = HashVertexVoxel(Vertices[Indices[i]].Position, voxelTolerance);
            long k1 = HashVertexVoxel(Vertices[Indices[i + 1]].Position, voxelTolerance);
            long k2 = HashVertexVoxel(Vertices[Indices[i + 2]].Position, voxelTolerance);

            ushort idx0 = remap[k0];
            ushort idx1 = remap[k1];
            ushort idx2 = remap[k2];

            // Only keep non-degenerate triangles
            if (idx0 != idx1 && idx1 != idx2 && idx0 != idx2)
            {
                newIndices.Add(idx0);
                newIndices.Add(idx1);
                newIndices.Add(idx2);
            }
        }

        Vertices = uniqueVertices;
        Indices = newIndices;
        RecalculateBounds();

        return initialCount - Vertices.Count;
    }

    /// <summary>
    /// Exports the mesh geometry into flat interleaved float buffers (Pos.xyz, Normal.xyz, UV.xy)
    /// structured for direct upload to WebGPU vertex buffers.
    /// </summary>
    public float[] ExportWebGpuVertexBuffer()
    {
        // 8 floats per vertex: [X, Y, Z, Nx, Ny, Nz, U, V]
        float[] buffer = new float[Vertices.Count * 8];
        for (int i = 0; i < Vertices.Count; i++)
        {
            int baseIdx = i * 8;
            var v = Vertices[i];
            buffer[baseIdx + 0] = v.Position.X;
            buffer[baseIdx + 1] = v.Position.Y;
            buffer[baseIdx + 2] = v.Position.Z;
            buffer[baseIdx + 3] = v.Normal.X;
            buffer[baseIdx + 4] = v.Normal.Y;
            buffer[baseIdx + 5] = v.Normal.Z;
            buffer[baseIdx + 6] = v.U;
            buffer[baseIdx + 7] = v.V;
        }
        return buffer;
    }

    private static long HashVertexVoxel(Vector3f pos, float tolerance)
    {
        int vx = (int)MathF.Round(pos.X / tolerance);
        int vy = (int)MathF.Round(pos.Y / tolerance);
        int vz = (int)MathF.Round(pos.Z / tolerance);
        return HashCode.Combine(vx, vy, vz);
    }
}
