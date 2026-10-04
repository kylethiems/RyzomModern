using Ryzom.Core.Math3D;
using Ryzom.Core.Spatial;
using Ryzom.Engine.Entities;
using Ryzom.Engine.Spatial;

namespace Ryzom.Tests;

public class MortonSpatialTests
{
    [Fact]
    public void EncodeDecode_RoundtripsAccurately()
    {
        uint x = 12345;
        uint y = 67890;
        uint z = 54321;

        ulong code = Morton3D.Encode(x, y, z);
        var (dx, dy, dz) = Morton3D.Decode(code);

        Assert.Equal(x, dx);
        Assert.Equal(y, dy);
        Assert.Equal(z, dz);
    }

    [Fact]
    public void VoxelSpatialGrid_RadiusQuery_FindsNearbyEntitiesOnly()
    {
        var grid = new VoxelSpatialGrid(cellSize: 32f);

        var player = new RyzomEntity { Id = 1, Name = "AtysWarrior", Position = new Vector3f(100f, 0f, 100f) };
        var nearbyMob = new RyzomEntity { Id = 2, Name = "Yubo", Position = new Vector3f(105f, 0f, 102f) };
        var distantMob = new RyzomEntity { Id = 3, Name = "Kirosta", Position = new Vector3f(500f, 0f, 500f) };

        grid.Insert(player);
        grid.Insert(nearbyMob);
        grid.Insert(distantMob);

        var results = new List<RyzomEntity>();
        grid.QueryRadius(player.Position, radius: 15.0f, results);

        Assert.Contains(results, e => e.Id == 1);
        Assert.Contains(results, e => e.Id == 2);
        Assert.DoesNotContain(results, e => e.Id == 3);
    }
}
