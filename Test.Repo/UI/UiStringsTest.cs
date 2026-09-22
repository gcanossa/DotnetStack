using System.Globalization;
using GKit.UI;
using GKit.UI.Localization;

namespace Test.Repo.UI;

public class UiStringsTest
{
  private static T WithCulture<T>(string culture, Func<T> fn)
  {
    var previous = CultureInfo.CurrentUICulture;
    try
    {
      CultureInfo.CurrentUICulture = new CultureInfo(culture);
      return fn();
    }
    finally
    {
      CultureInfo.CurrentUICulture = previous;
    }
  }

  [Fact]
  public void Default_IsNeutralEnglish()
  {
    var strings = new DefaultUiStrings();

    Assert.Equal("Edit", strings.Edit);
    Assert.Equal("Delete", strings.Delete);
    Assert.Equal("No records", strings.NoRecords);
  }

  [Fact]
  public void Resources_ResolveItalian()
  {
    var strings = new ResourceUiStrings();

    var edit = WithCulture("it-IT", () => strings.Edit);
    var noRecords = WithCulture("it-IT", () => strings.NoRecords);

    Assert.Equal("Modifica", edit);
    Assert.Equal("Nessun elemento presente", noRecords);
  }

  [Fact]
  public void Resources_UnsupportedCulture_FallsBackToNeutral()
  {
    var strings = new ResourceUiStrings();

    Assert.Equal("Edit", WithCulture("de-DE", () => strings.Edit));
  }

  [Fact]
  public void ConfirmDelete_EncodesInterpolatedEntityText()
  {
    // The prompt is rendered as markup, so entity-derived text must not be able to inject HTML.
    var strings = new DefaultUiStrings();

    var message = strings.ConfirmDelete("<script>alert(1)</script>");

    Assert.DoesNotContain("<script>", message);
    Assert.Contains("&lt;script&gt;", message);
    Assert.Contains("<strong>", message);
  }

  [Fact]
  public void ConfirmDelete_EncodesUnderItalianResources()
  {
    var strings = new ResourceUiStrings();

    var message = WithCulture("it-IT", () => strings.ConfirmDelete("Acme & Co <b>"));

    Assert.StartsWith("Confermi di voler eliminare", message);
    Assert.Contains("Acme &amp; Co &lt;b&gt;", message);
    Assert.DoesNotContain("<b>", message);
  }

  [Fact]
  public void ColumnFallback_IsFormattedPerCulture()
  {
    var strings = new ResourceUiStrings();

    Assert.Equal("Column 3", WithCulture("en", () => strings.ColumnFallback(3)));
    Assert.Equal("Colonna 3", WithCulture("it-IT", () => strings.ColumnFallback(3)));
  }

  [Fact]
  public void RowActionsMenu_IsTranslated()
  {
    // Replaces the stray English "Open user menu" aria-label the grid used to hard-code.
    var strings = new ResourceUiStrings();

    Assert.Equal("Row actions", WithCulture("en", () => strings.RowActionsMenu));
    Assert.Equal("Azioni riga", WithCulture("it-IT", () => strings.RowActionsMenu));
  }
}
