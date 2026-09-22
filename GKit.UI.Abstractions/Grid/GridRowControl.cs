namespace GKit.UI;

/// <summary>
/// The row a control is acting on. Replaces the component-library cell-context types, which
/// carried far more surface than the row controls ever used.
/// </summary>
public sealed record RowContext<T>(T Item, int RowIndex);

/// <summary>
/// A per-row action rendered by a grid, described independently of any component library so the
/// same descriptor list can be handed to either adapter.
/// </summary>
public class GridRowControl<T>
{
  public required string Text { get; set; }

  public required Func<RowContext<T>, Task> Action { get; set; }

  /// <summary>
  /// The icon to render, resolved per library through <see cref="IUiIconSet"/>.
  /// </summary>
  public UiIcon? Icon { get; set; }

  /// <summary>
  /// A library-native icon identifier, used in preference to <see cref="Icon"/> when set.
  /// The escape hatch for icons outside the shared <see cref="UiIcon"/> set.
  /// </summary>
  public string? IconOverride { get; set; }

  public UiColor Color { get; set; } = UiColor.Default;

  public Func<RowContext<T>, bool>? Disabled { get; set; }
}

/// <summary>
/// How a grid lays out its per-row controls.
/// </summary>
public enum GridRowControlsVariant
{
  /// <summary>One button per control, laid out in a row.</summary>
  Expanded,

  /// <summary>A single overflow button opening a menu of controls.</summary>
  Menu
}
