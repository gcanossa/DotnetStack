using Microsoft.Extensions.Logging;

namespace GKit.UI.Data;

/// <summary>
/// The search and display logic behind a lookup/autocomplete, where the bound value
/// (<typeparamref name="T"/>) and the searched item (<typeparamref name="TItem"/>) may differ -
/// binding an id while searching over entities, for instance.
/// </summary>
public sealed class EntityLookupEngine<T, TItem>(
  IUiNotifier notifier,
  IGKitUiStrings strings,
  ILogger logger)
{
  /// <summary>
  /// The items returned by the most recent search.
  /// </summary>
  /// <remarks>
  /// Exposed because the two libraries consume search results differently: MudBlazor's
  /// autocomplete takes the projected values back from the search call, while Radzen's dropdown
  /// binds its item list. Both need the same underlying result set.
  /// </remarks>
  public IReadOnlyList<TItem> LastResults { get; private set; } = [];

  /// <summary>Fetches candidates for the current search text. Required.</summary>
  public Func<string, CancellationToken, Task<IReadOnlyList<TItem>>>? Fetch { get; set; }

  /// <summary>Projects a searched item onto the bound value. Required.</summary>
  public Func<TItem, T>? ToValue { get; set; }

  /// <summary>Projects a bound value back onto an item, for values not in the last result set.</summary>
  public Func<T, TItem>? ToItem { get; set; }

  /// <summary>Renders an item for display.</summary>
  public Func<TItem?, string> ToStringFunc { get; set; } = item => item?.ToString() ?? "";

  /// <summary>
  /// Runs a search, surfacing failures to the user rather than faulting the component.
  /// </summary>
  /// <remarks>
  /// A lookup fires on every keystroke, so a transient failure must not tear down the form. The
  /// caller gets an empty result and the user gets a notification.
  /// </remarks>
  public async Task<IReadOnlyList<T>> SearchAsync(string searchText, CancellationToken token)
  {
    if (ToValue is null)
      throw new InvalidOperationException($"{nameof(ToValue)} must be set");

    return [.. (await SearchItemsAsync(searchText, token)).Select(ToValue)];
  }

  /// <summary>
  /// Runs a search and returns the matching items, also recording them in
  /// <see cref="LastResults"/>.
  /// </summary>
  public async Task<IReadOnlyList<TItem>> SearchItemsAsync(string searchText, CancellationToken token)
  {
    if (Fetch is null)
      throw new InvalidOperationException($"{nameof(Fetch)} must be set");

    try
    {
      LastResults = await Fetch(searchText, token);
    }
    catch (OperationCanceledException)
    {
      LastResults = [];
    }
    catch (Exception e)
    {
      logger.LogWarning(e, "Could not fetch lookup values for {ItemType}", typeof(TItem).Name);
      notifier.Error(strings.FetchValuesFailed);
      LastResults = [];
    }

    return LastResults;
  }

  /// <summary>
  /// Resolves the bound value back to an item, preferring the last result set so the display text
  /// survives a value that was selected before the current search.
  /// </summary>
  public TItem? GetByValue(T? value)
  {
    if (value is null)
      return default;

    if (ToValue is not null)
    {
      var match = LastResults.FirstOrDefault(item => value.Equals(ToValue(item)));
      if (match is not null)
        return match;
    }

    return ToItem is not null ? ToItem(value) : default;
  }

  /// <summary>Display text for a bound value.</summary>
  public string Display(T? value) => ToStringFunc(GetByValue(value));
}
