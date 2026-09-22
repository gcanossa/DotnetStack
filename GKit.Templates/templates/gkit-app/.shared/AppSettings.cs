namespace GKit.App1;

/// <summary>
/// Bound to the "App" configuration section. Every GKit reference application keeps its own
/// settings here rather than reading IConfiguration directly, so the shape is validated once.
/// </summary>
public class AppSettings
{
    public string? AttachmentsPath { get; set; }
}
