using System.Text.RegularExpressions;

namespace GKit.Cli.Commands;

/// <summary>
/// Codemod for section 9 of docs/ui-abstraction-proposal.md. Source compatibility was deliberately
/// not preserved there, and the reference applications are unmigrated.
/// <para>
/// Mechanical renames are applied; anything needing judgement is reported and left alone. Silently
/// rewriting a grid column whose sort expression calls a method would produce code that compiles
/// and then fails at runtime under Dynamic LINQ.
/// </para>
/// </summary>
public static class MigrateUiCommand
{
  private static readonly string[] SourceExtensions = [".cs", ".razor"];

  public record Change(string File, string From, string To, int Count);

  public record Report(string File, int Line, string Message, string Snippet);

  public record Result(IReadOnlyList<Change> Changes, IReadOnlyList<Report> Reports, int PackagesRenamed);

  public static Result Analyze(Workspace workspace, FeatureManifest manifest, bool apply)
  {
    var renames = manifest.UiRenames;
    var reportPatterns = manifest.UiReports
      .Select(p => (Regex: new Regex(p.Pattern, RegexOptions.Compiled), p.Message))
      .ToList();

    var changes = new List<Change>();
    var reports = new List<Report>();

    foreach (var file in EnumerateSourceFiles(workspace.Root))
    {
      var original = File.ReadAllText(file);
      var text = original;
      var relative = workspace.RelativeTo(file);

      foreach (var rename in renames)
      {
        // Package ids are handled on the csproj pass; here the same string is a namespace.
        var pattern = rename.Kind == "type"
          ? Regex.Escape(rename.From)
          : $@"\b{Regex.Escape(rename.From)}";

        var count = Regex.Matches(text, pattern).Count;
        if (count == 0) continue;

        text = Regex.Replace(text, pattern, rename.To.Replace("$", "$$"));
        changes.Add(new Change(relative, rename.From, rename.To, count));
      }

      foreach (var (regex, message) in reportPatterns)
      {
        foreach (Match match in regex.Matches(original))
        {
          var line = original.Take(match.Index).Count(c => c == '\n') + 1;
          reports.Add(new Report(relative, line, message, Snippet(match.Value)));
        }
      }

      if (apply && text != original) File.WriteAllText(file, text);
    }

    var packagesRenamed = 0;
    var retired = manifest.RetiredPackages;

    foreach (var projectPath in workspace.ProjectPaths)
    {
      var project = ProjectFile.Load(projectPath);
      var changed = false;

      foreach (var (from, replacement) in retired)
      {
        if (!project.RenamePackage(from, replacement.Replacement)) continue;

        changed = true;
        packagesRenamed++;
        changes.Add(new Change(workspace.RelativeTo(projectPath), from, replacement.Replacement, 1));
      }

      if (changed && apply) project.Save();
    }

    return new Result(changes, reports, packagesRenamed);
  }

  public static int Run(Workspace workspace, FeatureManifest manifest, bool apply, TextWriter output)
  {
    var result = Analyze(workspace, manifest, apply);

    if (result.Changes.Count == 0 && result.Reports.Count == 0)
    {
      output.WriteLine("Nothing to migrate.");
      return 0;
    }

    if (result.Changes.Count > 0)
    {
      output.WriteLine(apply ? "Applied:" : "Would apply (pass --apply to write):");

      foreach (var group in result.Changes.GroupBy(p => p.File).OrderBy(p => p.Key, StringComparer.Ordinal))
      {
        output.WriteLine($"  {group.Key}");
        foreach (var change in group)
          output.WriteLine($"    {change.From} -> {change.To}  ({change.Count})");
      }
    }

    if (result.Reports.Count > 0)
    {
      output.WriteLine();
      output.WriteLine("Needs a human - not rewritten:");

      foreach (var group in result.Reports.GroupBy(p => p.Message))
      {
        output.WriteLine($"  {group.Key}");
        foreach (var report in group.OrderBy(p => p.File, StringComparer.Ordinal))
          output.WriteLine($"    {report.File}:{report.Line}  {report.Snippet}");
      }
    }

    return 0;
  }

  private static IEnumerable<string> EnumerateSourceFiles(string root) =>
    Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
      .Where(p => SourceExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
      .Where(p => !Workspace.IsBuildOutput(p))
      .OrderBy(p => p, StringComparer.Ordinal);

  private static string Snippet(string value)
  {
    var single = Regex.Replace(value, @"\s+", " ").Trim();
    return single.Length <= 90 ? single : single[..87] + "...";
  }
}
