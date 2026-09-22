namespace GKit.Cli.Commands;

/// <summary>
/// Swaps GKit PackageReferences for ProjectReferences into a local checkout, and back.
/// </summary>
public static class LinkCommand
{
  public static int Run(Workspace workspace, FeatureManifest manifest, string? repositoryPath,
      IReadOnlyList<string> only, bool addToSolution, TextWriter output)
  {
    if (LinkState.TryLoad(workspace.Root) is not null)
    {
      output.WriteLine("Already linked. Run 'gkit unlink' first.");
      return 1;
    }

    var repository = repositoryPath is not null
      ? GKitRepository.TryOpen(repositoryPath)
      : GKitRepository.AutoDetect(workspace.Root);

    if (repository is null)
    {
      output.WriteLine(repositoryPath is not null
        ? $"No GKit checkout at {repositoryPath}: expected GKit.<name>/GKit.<name>.csproj directories."
        : "No GKit checkout found nearby. Pass --path <dir>.");
      return 1;
    }

    var wanted = only.Count > 0
      ? only.ToHashSet(StringComparer.OrdinalIgnoreCase)
      : manifest.GKitPackages.ToHashSet(StringComparer.OrdinalIgnoreCase);

    var state = new LinkState { RepositoryPath = repository.Root };
    var linkedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var projectPath in workspace.ProjectPaths)
    {
      var project = ProjectFile.Load(projectPath);
      var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
      var changed = false;

      foreach (var reference in project.PackageReferences.ToList())
      {
        if (!wanted.Contains(reference.Id)) continue;
        if (!repository.ProjectsByPackageId.TryGetValue(reference.Id, out var target)) continue;

        var relative = Path.GetRelativePath(projectDirectory, target).Replace('\\', '/');
        var version = project.SwapPackageForProject(reference.Id, relative);
        if (version is null) continue;

        state.Links.Add(new LinkState.LinkEntry
        {
          Project = workspace.RelativeTo(projectPath),
          Package = reference.Id,
          Version = version,
          Reference = relative
        });

        linkedProjects.Add(target);
        changed = true;
        output.WriteLine($"  {workspace.RelativeTo(projectPath)}: {reference.Id} -> {relative}");
      }

      if (changed) project.Save();
    }

    if (state.Links.Count == 0)
    {
      output.WriteLine("No GKit package references found to link.");
      return 0;
    }

    state.Save(workspace.Root);

    if (addToSolution && workspace.SolutionPath is not null)
    {
      var editor = new SolutionEditor(workspace.SolutionPath);
      foreach (var target in linkedProjects.OrderBy(p => p, StringComparer.Ordinal))
      {
        if (editor.Add(target)) output.WriteLine($"  solution += {Path.GetFileName(target)}");
      }
    }

    output.WriteLine($"Linked {state.Links.Count} reference(s) against {repository.Root}.");
    return 0;
  }

  public static int Unlink(Workspace workspace, bool removeFromSolution, TextWriter output)
  {
    var state = LinkState.TryLoad(workspace.Root);
    if (state is null)
    {
      output.WriteLine($"Not linked: no {LinkState.FileName} at {workspace.Root}.");
      return 1;
    }

    var restored = 0;

    foreach (var group in state.Links.GroupBy(p => p.Project))
    {
      var projectPath = Path.Combine(workspace.Root, group.Key);
      if (!File.Exists(projectPath))
      {
        output.WriteLine($"  skipped {group.Key}: no longer present");
        continue;
      }

      var project = ProjectFile.Load(projectPath);
      var changed = false;

      foreach (var entry in group)
      {
        if (!project.SwapProjectForPackage(entry.Reference, entry.Package, entry.Version)) continue;

        changed = true;
        restored++;
        output.WriteLine($"  {entry.Project}: {entry.Package} <- package {entry.Version}");
      }

      if (changed) project.Save();
    }

    if (removeFromSolution && workspace.SolutionPath is not null)
    {
      var editor = new SolutionEditor(workspace.SolutionPath);
      var targets = state.Links
        .Select(p => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.Combine(workspace.Root, p.Project))!, p.Reference)))
        .Distinct();

      foreach (var target in targets)
      {
        if (editor.Remove(target)) output.WriteLine($"  solution -= {Path.GetFileName(target)}");
      }
    }

    LinkState.Delete(workspace.Root);

    output.WriteLine($"Restored {restored} package reference(s).");
    return 0;
  }
}
