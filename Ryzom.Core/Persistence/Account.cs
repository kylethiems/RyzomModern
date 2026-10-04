using System.ComponentModel.DataAnnotations;

namespace Ryzom.Core.Persistence;

public class Account
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Character> Characters { get; set; } = new();
}
