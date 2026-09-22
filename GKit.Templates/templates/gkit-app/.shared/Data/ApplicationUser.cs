using GKit.Authentication.Simple;
using GKit.EntityFramework;

namespace GKit.App1.Data;

public class ApplicationUser : ISimpleAccountUser, ISoftDeletableEntity
{
    public int Id { get; set; }

    public required string AccountName { get; set; }
    public string? PasswordHash { get; set; }
    public string? Mail { get; set; }
    public string? FullName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAt { get; set; }

    // ISimpleAccountUser maps these onto the NameIdentifier and Name claims.
    public string Identifier => Id.ToString();
    string? ISimpleAccountUser.DisplayName => FullName;
}
