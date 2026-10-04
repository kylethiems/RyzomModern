using Ryzom.Core.Math3D;
using Ryzom.Engine.Entities;
using Ryzom.Engine.Spatial;

namespace Ryzom.Engine.Ecology;

public class CreatureHerd
{
    public string Species { get; set; } = "Yubo";
    public List<RyzomEntity> Members { get; set; } = new();
    public Vector3f TerritoryCenter { get; set; }
    public float TerritoryRadius { get; set; } = 150.0f;
}

public class EcologySimulation
{
    private readonly VoxelSpatialGrid _spatialGrid;
    private readonly List<CreatureHerd> _herds = new();
    private readonly Random _random = new(42); // Deterministic seed

    public EcologySimulation(VoxelSpatialGrid spatialGrid)
    {
        _spatialGrid = spatialGrid;
        InitializeDefaultHerds();
    }

    public IReadOnlyList<CreatureHerd> Herds => _herds;

    private void InitializeDefaultHerds()
    {
        // Yubo herbivore herd near Fyros desert oasis
        var yuboHerd = new CreatureHerd
        {
            Species = "Yubo",
            TerritoryCenter = new Vector3f(150f, 0f, 150f),
            TerritoryRadius = 80f
        };

        for (uint i = 1; i <= 8; i++)
        {
            var yubo = new RyzomEntity
            {
                Id = 2000 + i,
                Name = $"Yubo_{i}",
                Type = EntityType.Creature,
                HitPoints = 400,
                MaxHitPoints = 400,
                Position = new Vector3f(150f + (i * 5f), 0f, 150f + (i * 3f)),
                MovementSpeed = 2.5f
            };
            yuboHerd.Members.Add(yubo);
            _spatialGrid.Insert(yubo);
        }
        _herds.Add(yuboHerd);

        // Kitin predator patrol
        var kitinPatrol = new CreatureHerd
        {
            Species = "Kitin Larva",
            TerritoryCenter = new Vector3f(350f, 0f, 350f),
            TerritoryRadius = 120f
        };

        for (uint i = 1; i <= 4; i++)
        {
            var kitin = new RyzomEntity
            {
                Id = 3000 + i,
                Name = $"Kitin_{i}",
                Type = EntityType.Creature,
                HitPoints = 650,
                MaxHitPoints = 650,
                Position = new Vector3f(350f + (i * 8f), 0f, 350f + (i * 4f)),
                MovementSpeed = 5.0f
            };
            kitinPatrol.Members.Add(kitin);
            _spatialGrid.Insert(kitin);
        }
        _herds.Add(kitinPatrol);
    }

    public void Tick(float deltaSeconds)
    {
        foreach (var herd in _herds)
        {
            foreach (var creature in herd.Members)
            {
                if (!creature.IsAlive) continue;

                // Simple wandering vector
                float wanderX = (_random.NextSingle() - 0.5f) * creature.MovementSpeed * deltaSeconds;
                float wanderZ = (_random.NextSingle() - 0.5f) * creature.MovementSpeed * deltaSeconds;

                var newPos = new Vector3f(creature.Position.X + wanderX, creature.Position.Y, creature.Position.Z + wanderZ);

                // Clamp to territory bounds
                if (Vector3f.Distance(newPos, herd.TerritoryCenter) > herd.TerritoryRadius)
                {
                    // Turn back towards center
                    var dir = (herd.TerritoryCenter - creature.Position).Normalize();
                    newPos = creature.Position + dir * (creature.MovementSpeed * deltaSeconds);
                }

                _spatialGrid.Update(creature, newPos);
                creature.Regenerate(deltaSeconds);
            }
        }
    }
}
