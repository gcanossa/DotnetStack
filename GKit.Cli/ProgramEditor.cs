namespace GKit.Cli;

/// <summary>
/// Inserts wiring into a generated Program.cs at the anchors the gkit-app template establishes.
/// <para>
/// Text editing rather than syntax rewriting on purpose: Program.cs is top level statements a
/// developer reads top to bottom, and a Roslyn round trip would reformat the whole file. Every
/// operation is idempotent, so running <c>gkit add</c> twice is harmless.
/// </para>
/// </summary>
public class ProgramEditor(string text)
{
  private string _text = text.Replace("\r\n", "\n");

  public string Text => _text;

  public bool Changed { get; private set; }

  public static ProgramEditor Load(string path) => new(File.ReadAllText(path));

  public void Save(string path) => File.WriteAllText(path, _text);

  public bool AddUsing(string @namespace)
  {
    var line = $"using {@namespace};";
    if (_text.Contains(line)) return false;

    var lines = _text.Split('\n').ToList();
    var lastUsing = lines.FindLastIndex(p => p.StartsWith("using ") && p.TrimEnd().EndsWith(';'));
    if (lastUsing < 0) lastUsing = -1;

    lines.Insert(lastUsing + 1, line);
    _text = string.Join('\n', lines);
    Changed = true;
    return true;
  }

  /// <summary>Inserts a service registration just before the host is built.</summary>
  public bool AddServiceCall(string statement) =>
    InsertBefore(statement, p => p.Contains("var app = builder.Build()") || p.Contains("builder.Build()"));

  /// <summary>Inserts a call on the app, just before the host is handed back to Application.Wrap.</summary>
  public bool AddAppCall(string statement) =>
    InsertBefore(statement, p => p.TrimStart().StartsWith("return app;"));

  /// <summary>
  /// Appends a check to the chain the template opens with <c>var healthChecks = ...</c>.
  /// Returns false when the application was scaffolded without health checks.
  /// </summary>
  public bool AddHealthCheck(string expression)
  {
    var statement = $"healthChecks{expression};";
    if (_text.Contains(statement)) return false;

    var lines = _text.Split('\n').ToList();
    var anchor = lines.FindIndex(p => p.Contains("var healthChecks ="));
    if (anchor < 0) return false;

    // Keep the chain together: append after the last existing healthChecks line.
    var last = anchor;
    for (var i = anchor + 1; i < lines.Count && lines[i].TrimStart().StartsWith("healthChecks"); i++) last = i;

    lines.Insert(last + 1, Indent(lines[anchor]) + statement);
    _text = string.Join('\n', lines);
    Changed = true;
    return true;
  }

  private bool InsertBefore(string statement, Func<string, bool> anchorPredicate)
  {
    if (_text.Contains(statement.Trim())) return false;

    var lines = _text.Split('\n').ToList();
    var anchor = lines.FindIndex(p => anchorPredicate(p));
    if (anchor < 0) return false;

    var indent = Indent(lines[anchor]);
    var inserted = statement.Split('\n').Select(p => p.Length == 0 ? p : indent + p).ToList();
    inserted.Add("");

    lines.InsertRange(anchor, inserted);
    _text = string.Join('\n', lines);
    Changed = true;
    return true;
  }

  private static string Indent(string line) => new(line.TakeWhile(char.IsWhiteSpace).ToArray());
}
