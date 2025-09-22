namespace GKit.Settings;

public class Setting
{
    public required string Key { get; set; }
    public string? Value { get; set; }
    public required string TypeName { get; set; }
}