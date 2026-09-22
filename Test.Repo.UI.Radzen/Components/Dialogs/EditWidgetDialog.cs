using GKit.UI.RadzenExt;
using Test.Repo.UI.Radzen.Components.Forms;
using Test.Repo.UI.Shared;

namespace Test.Repo.UI.Radzen.Components.Dialogs;

/// <summary>
/// Identical to the MudBlazor host's EditWidgetDialog except for the base type's namespace - the
/// factory body comes from the shared layer either way.
/// </summary>
public class EditWidgetDialog : EditEntityDialog<Widget, EditWidgetForm, WidgetValidator>
{
  public override Widget EmptyValueFactory() => DemoQueries.EmptyWidget();
}
