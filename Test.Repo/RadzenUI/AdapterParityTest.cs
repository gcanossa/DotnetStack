using GKit.UI;
using GKit.UI.Data;
using Microsoft.EntityFrameworkCore;
using Test.Repo.UI.Shared;
using MudDialog = GKit.UI.MudBlazorExt.EditEntityDialog<Test.Repo.UI.Shared.Widget,
  Test.Repo.UI.MudBlazorExt.Components.Forms.EditWidgetForm, Test.Repo.UI.Shared.WidgetValidator>;
using RadzenDialog = GKit.UI.RadzenExt.EditEntityDialog<Test.Repo.UI.Shared.Widget,
  Test.Repo.UI.RadzenExt.Components.Forms.EditWidgetForm, Test.Repo.UI.Shared.WidgetValidator>;
using MudWidgetDialog = Test.Repo.UI.MudBlazorExt.Components.Dialogs.EditWidgetDialog;
using RadzenWidgetDialog = Test.Repo.UI.RadzenExt.Components.Dialogs.EditWidgetDialog;

namespace Test.Repo.RadzenUI;

/// <summary>
/// Checks the claims the dual-adapter design rests on: that the two adapters share the engine,
/// the contracts and the domain layer, and only diverge in markup.
/// </summary>
public class AdapterParityTest
{
  [Fact]
  public void BothDialogTypesImplementTheSameNeutralContract()
  {
    Assert.True(typeof(IEditEntityDialog<Widget>).IsAssignableFrom(typeof(MudWidgetDialog)));
    Assert.True(typeof(IEditEntityDialog<Widget>).IsAssignableFrom(typeof(RadzenWidgetDialog)));
  }

  [Fact]
  public void BothDialogTypesDeriveFromTheirAdaptersShellOverTheSameFormAndValidator()
  {
    // Same T and same TValidator on both sides; only the shell and the form markup differ.
    Assert.True(typeof(MudDialog).IsAssignableFrom(typeof(MudWidgetDialog)));
    Assert.True(typeof(RadzenDialog).IsAssignableFrom(typeof(RadzenWidgetDialog)));
  }

  [Fact]
  public void BothHostsUseTheSameValidatorType()
  {
    var mudValidator = typeof(MudWidgetDialog).BaseType!.GetGenericArguments()[2];
    var radzenValidator = typeof(RadzenWidgetDialog).BaseType!.GetGenericArguments()[2];

    Assert.Same(mudValidator, radzenValidator);
    Assert.Same(typeof(WidgetValidator), mudValidator);
  }

  [Fact]
  public void BothHostsUseTheSameEntityType()
  {
    var mudEntity = typeof(MudWidgetDialog).BaseType!.GetGenericArguments()[0];
    var radzenEntity = typeof(RadzenWidgetDialog).BaseType!.GetGenericArguments()[0];

    Assert.Same(mudEntity, radzenEntity);
  }

  [Fact]
  public void BothEmptyValueFactoriesProduceTheSameSeed()
  {
    var mud = (IEditEntityDialog<Widget>)Activator.CreateInstance<MudWidgetDialog>();
    var radzen = (IEditEntityDialog<Widget>)Activator.CreateInstance<RadzenWidgetDialog>();

    var fromMud = mud.EmptyValueFactory();
    var fromRadzen = radzen.EmptyValueFactory();

    // Both delegate to DemoQueries.EmptyWidget in the shared layer.
    Assert.Equal(fromMud.Name, fromRadzen.Name);
    Assert.Equal(fromMud.Quantity, fromRadzen.Quantity);
  }

  [Fact]
  public void SharedProjectReferencesNeitherAdapter()
  {
    // The compiler already enforces this, but stating it makes the intent a test rather than a
    // convention someone can quietly break.
    var referenced = typeof(DemoQueries).Assembly
      .GetReferencedAssemblies()
      .Select(a => a.Name)
      .ToList();

    Assert.DoesNotContain("GKit.UI.MudBlazorExt", referenced);
    Assert.DoesNotContain("GKit.UI.RadzenExt", referenced);
    Assert.DoesNotContain("MudBlazor", referenced);
    Assert.DoesNotContain("Radzen.Blazor", referenced);
  }

  [Fact]
  public async Task BothAdaptersLoadIdenticalDataThroughTheSharedEngine()
  {
    // One engine configuration, exercised the way each adapter's grid would.
    var db = nameof(BothAdaptersLoadIdenticalDataThroughTheSharedEngine);
    using var seed = DemoData.Seed(db);

    EntityGridEngine<Widget> NewEngine() => new()
    {
      ContextFactory = () => DemoData.Create(db),
      QueryFactory = DemoQueries.Widgets
    };

    var query = new GridQuery<Widget>
    {
      StartIndex = 0,
      Count = 5,
      NativeSort = q => q.OrderBy(w => w.Id)
    };

    var first = await NewEngine().LoadAsync(query, default);
    var second = await NewEngine().LoadAsync(query, default);

    Assert.Equal(first.TotalItems, second.TotalItems);
    Assert.Equal(first.Items.Select(w => w.Name), second.Items.Select(w => w.Name));
  }

  [Theory]
  [InlineData("", false)]
  [InlineData("Valid name", true)]
  public async Task TheSameValidatorGovernsBothAdapters(string name, bool expectedValid)
  {
    // MudBlazor calls this signature directly from MudForm; the Radzen adapter drives the same
    // validator through GKitFluentValidator on an EditContext.
    var validator = new WidgetValidator();
    var widget = new Widget { Name = name, Quantity = 1 };

    var errors = await validator.ValidateValueAsync(widget, nameof(Widget.Name));

    Assert.Equal(expectedValid, !errors.Any());
  }

  [Fact]
  public void NeutralGridQueryCarriesNoComponentLibraryTypes()
  {
    // The property that makes application data plumbing portable.
    var assembly = typeof(GridQuery<Widget>).Assembly;

    var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

    Assert.DoesNotContain("MudBlazor", referenced);
    Assert.DoesNotContain("Radzen.Blazor", referenced);
  }
}
