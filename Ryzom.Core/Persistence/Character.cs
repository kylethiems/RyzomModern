using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Ryzom.Core.Math3D;

namespace Ryzom.Core.Persistence;

public enum HominRace
{
    Fyros = 1,
    Matis = 2,
    Tryker = 3,
    Zorai = 4
}

public class Character
{
    public int Id { get; set; }

    public int AccountId { get; set; }
    public Account? Account { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public HominRace Race { get; set; } = HominRace.Fyros;

    public int Level { get; set; } = 1;

    // Tri-vitals
    public int HitPoints { get; set; } = 1000;
    public int MaxHitPoints { get; set; } = 1000;

    public int Sap { get; set; } = 500;
    public int MaxSap { get; set; } = 500;

    public int Stamina { get; set; } = 800;
    public int MaxStamina { get; set; } = 800;

    // World coordinates
    [Column(TypeName = "decimal(12,4)")]
    public decimal PositionX { get; set; } = 0.0000m;

    [Column(TypeName = "decimal(12,4)")]
    public decimal PositionY { get; set; } = 0.0000m;

    [Column(TypeName = "decimal(12,4)")]
    public decimal PositionZ { get; set; } = 0.0000m;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<InventoryItem> Inventory { get; set; } = new();

    [NotMapped]
    public Vector3f Position => new((float)PositionX, (float)PositionY, (float)PositionZ);

    public void SetPosition(Vector3f pos)
    {
        PositionX = (decimal)pos.X;
        PositionY = (decimal)pos.Y;
        PositionZ = (decimal)pos.Z;
    }
}
