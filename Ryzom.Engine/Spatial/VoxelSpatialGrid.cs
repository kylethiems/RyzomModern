using System.Collections.Concurrent;
using Ryzom.Core.Math3D;
using Ryzom.Core.Spatial;
using Ryzom.Engine.Entities;

namespace Ryzom.Engine.Spatial;

/// <summary>
/// Flat Morton-indexed 3D spatial partitioning grid.
/// Delivers O(1) cell insertion and cache-coherent radius queries for high-density MMORPG clusters.
/// </summary>
public class VoxelSpatialGrid
{
    private readonly float _cellSize;
    private readonly float _worldMin;
    private readonly ConcurrentDictionary<ulong, List<RyzomEntity>> _buckets = new();
    private readonly ConcurrentDictionary<uint, ulong> _entityBuckets = new();

    public VoxelSpatialGrid(float cellSize = 32.0f, float worldMin = -16384.0f)
    {
        _cellSize = cellSize;
        _worldMin = worldMin;
    }

    public void Insert(RyzomEntity entity)
    {
        ulong mortonCode = Morton3D.QuantizeAndEncode(
            entity.Position.X, entity.Position.Y, entity.Position.Z, _worldMin, _cellSize);

        _buckets.AddOrUpdate(mortonCode,
            _ => new List<RyzomEntity> { entity },
            (_, list) =>
            {
                lock (list) list.Add(entity);
                return list;
            });

        _entityBuckets[entity.Id] = mortonCode;
    }

    public void Remove(RyzomEntity entity)
    {
        if (_entityBuckets.TryRemove(entity.Id, out ulong mortonCode))
        {
            if (_buckets.TryGetValue(mortonCode, out var list))
            {
                lock (list) list.Remove(entity);
            }
        }
    }

    public void Update(RyzomEntity entity, Vector3f newPosition)
    {
        ulong newCode = Morton3D.QuantizeAndEncode(newPosition.X, newPosition.Y, newPosition.Z, _worldMin, _cellSize);

        if (_entityBuckets.TryGetValue(entity.Id, out ulong oldCode))
        {
            if (oldCode == newCode)
            {
                entity.Position = newPosition;
                return;
            }

            // Moved into a new voxel cell: remove from old, add to new
            if (_buckets.TryGetValue(oldCode, out var oldList))
            {
                lock (oldList) oldList.Remove(entity);
            }
        }

        entity.Position = newPosition;
        _buckets.AddOrUpdate(newCode,
            _ => new List<RyzomEntity> { entity },
            (_, list) =>
            {
                lock (list) list.Add(entity);
                return list;
            });

        _entityBuckets[entity.Id] = newCode;
    }

    public void QueryRadius(Vector3f center, float radius, List<RyzomEntity> results)
    {
        results.Clear();
        float radiusSq = radius * radius;

        int minCellX = (int)MathF.Floor((center.X - radius - _worldMin) / _cellSize);
        int maxCellX = (int)MathF.Ceiling((center.X + radius - _worldMin) / _cellSize);
        int minCellY = (int)MathF.Floor((center.Y - radius - _worldMin) / _cellSize);
        int maxCellY = (int)MathF.Ceiling((center.Y + radius - _worldMin) / _cellSize);
        int minCellZ = (int)MathF.Floor((center.Z - radius - _worldMin) / _cellSize);
        int maxCellZ = (int)MathF.Ceiling((center.Z + radius - _worldMin) / _cellSize);

        for (int x = minCellX; x <= maxCellX; x++)
        {
            for (int y = minCellY; y <= maxCellY; y++)
            {
                for (int z = minCellZ; z <= maxCellZ; z++)
                {
                    if (x < 0 || y < 0 || z < 0) continue;

                    ulong code = Morton3D.Encode((uint)x, (uint)y, (uint)z);
                    if (_buckets.TryGetValue(code, out var list))
                    {
                        lock (list)
                        {
                            foreach (var entity in list)
                            {
                                if (Vector3f.DistanceSquared(center, entity.Position) <= radiusSq)
                                {
                                    results.Add(entity);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
