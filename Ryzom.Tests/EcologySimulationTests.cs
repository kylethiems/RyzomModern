using Ryzom.Core.Math3D;
using Ryzom.Engine.Ecology;
using Ryzom.Engine.Spatial;

namespace Ryzom.Tests;

public class EcologySimulationTests
{
    [Fact]
    public void EcologySimulation_HerdsMoveWithinTerritory()
    {
        var grid = new VoxelSpatialGrid(cellSize: 32f);
        var ecology = new EcologySimulation(grid);

        Assert.NotEmpty(ecology.Herds);
        var yuboHerd = ecology.Herds.First(h => h.Species == "Yubo");
        Assert.NotEmpty(yuboHerd.Members);

        var firstYubo = yuboHerd.Members[0];
        var initialPos = firstYubo.Position;

        // Tick simulation by 2.0 seconds
        ecology.Tick(2.0f);

        // Position should have changed (wandered)
        Assert.NotEqual(initialPos, firstYubo.Position);

        // But must remain bounded within the herd territory radius
        float distFromCenter = Vector3f.Distance(firstYubo.Position, yuboHerd.TerritoryCenter);
        Assert.True(distFromCenter <= yuboHerd.TerritoryRadius + 1.0f);
    }
}
