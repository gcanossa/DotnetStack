namespace GKit.UI;

/// <summary>
/// The semantic colours shared by the supported component libraries.
/// </summary>
public enum UiColor
{
  Default,
  Primary,
  Secondary,
  Info,
  Success,
  Warning,
  Error
}

/// <summary>
/// The icons GKit's own components need. Deliberately small: anything outside this set goes
/// through <see cref="GridRowControl{T}.IconOverride"/> as a library-native identifier.
/// </summary>
public enum UiIcon
{
  Add,
  Edit,
  Delete,
  Download,
  Refresh,
  More,
  DragIndicator,
  Visibility,
  VisibilityOff,
  ContentCopy,
  Check,
  Close,
  Search
}

/// <summary>
/// Resolves a <see cref="UiIcon"/> to the identifier its component library expects - an SVG path
/// for MudBlazor, a Material Symbols ligature name for Radzen.
/// </summary>
public interface IUiIconSet
{
  string Resolve(UiIcon icon);
}
