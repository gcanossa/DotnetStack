using GKit.EntityFramework;

namespace GKIT-SHARED-NS;

public class Sample : ISoftDeletableEntity
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
}
