using Ryzom.Core.Formats;
using Ryzom.Core.Math3D;

namespace Ryzom.Core.Graphics;

public struct VertexPbr
{
    public Vector3f Position;
    public Vector3f Normal;
    public Vector3f Tangent;
    public float U;
    public float V;

    public VertexPbr(Vector3f pos, Vector3f normal, Vector3f tangent, float u, float v)
    {
        Position = pos;
        Normal = normal;
        Tangent = tangent;
        U = u;
        V = v;
    }
}

public static class TangentSpaceGenerator
{
    /// <summary>
    /// Computes accurate tangent vectors for all vertices in a triangular mesh.
    /// Essential for rendering tangent-space normal maps without lighting distortion.
    /// </summary>
    public static List<VertexPbr> GenerateTangents(NeLMesh mesh)
    {
        int vertexCount = mesh.Vertices.Count;
        var tangents = new Vector3f[vertexCount];
        var bitangents = new Vector3f[vertexCount];

        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            if (i + 2 >= mesh.Indices.Count) break;

            int i0 = mesh.Indices[i + 0];
            int i1 = mesh.Indices[i + 1];
            int i2 = mesh.Indices[i + 2];

            var v0 = mesh.Vertices[i0];
            var v1 = mesh.Vertices[i1];
            var v2 = mesh.Vertices[i2];

            var edge1 = v1.Position - v0.Position;
            var edge2 = v2.Position - v0.Position;

            float deltaU1 = v1.U - v0.U;
            float deltaV1 = v1.V - v0.V;
            float deltaU2 = v2.U - v0.U;
            float deltaV2 = v2.V - v0.V;

            float denom = deltaU1 * deltaV2 - deltaU2 * deltaV1;
            float f = MathF.Abs(denom) > 1e-6f ? 1.0f / denom : 0.0f;

            var tangent = new Vector3f(
                f * (deltaV2 * edge1.X - deltaV1 * edge2.X),
                f * (deltaV2 * edge1.Y - deltaV1 * edge2.Y),
                f * (deltaV2 * edge1.Z - deltaV1 * edge2.Z)
            );

            tangents[i0] += tangent;
            tangents[i1] += tangent;
            tangents[i2] += tangent;
        }

        var result = new List<VertexPbr>(vertexCount);
        for (int i = 0; i < vertexCount; i++)
        {
            var v = mesh.Vertices[i];
            var n = v.Normal;
            var t = tangents[i];

            // Gram-Schmidt orthogonalize: t' = Normalize(t - n * Dot(n, t))
            var orthoTangent = (t - n * Vector3f.Dot(n, t)).Normalize();
            if (orthoTangent.LengthSquared() < 1e-4f)
            {
                orthoTangent = Vector3f.UnitX;
            }

            result.Add(new VertexPbr(v.Position, v.Normal, orthoTangent, v.U, v.V));
        }

        return result;
    }
}
