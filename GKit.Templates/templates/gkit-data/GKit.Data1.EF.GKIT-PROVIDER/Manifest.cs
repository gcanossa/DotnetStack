namespace GKit.Data1.EF.GKIT-PROVIDER;

/// <summary>
/// Exists only so the host can point MigrationsAssembly at this assembly by type rather than by
/// string:
/// <code>
/// options.UseX(connectionString,
///   b =&gt; b.MigrationsAssembly(typeof(Manifest).Assembly.FullName));
/// </code>
/// An assembly name typed as a literal silently stops matching the day a project is renamed.
/// </summary>
public class Manifest
{
}
