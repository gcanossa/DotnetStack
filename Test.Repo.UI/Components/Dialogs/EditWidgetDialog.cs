using GKit.UI.MudBlazorExt;
using Test.Repo.UI.Components.Forms;
using Test.Repo.UI.Shared;

namespace Test.Repo.UI.Components.Dialogs;

/// <summary>
/// The dialog shell is adapter-bound, but its body is not: only the base type changes between
/// adapters, and <see cref="EmptyValueFactory"/> comes straight from the shared layer.
/// </summary>
public class EditWidgetDialog : EditEntityDialog<Widget, EditWidgetForm, WidgetValidator>
{
  public override Widget EmptyValueFactory() => DemoQueries.EmptyWidget();
}
