using GKit.UI.RadzenExt;
using GKIT-HOST-NS.Forms;
using GKIT-SHARED-NS;

namespace GKIT-HOST-NS.Dialogs;

/// <summary>
/// The dialog shell is adapter bound, but its body is not: only the base type's namespace changes
/// between adapters, and the factory comes straight from the shared layer.
/// </summary>
public class EditSampleDialog : EditEntityDialog<Sample, EditSampleForm, SampleValidator>
{
    public override Sample EmptyValueFactory() => SampleQueries.Empty();
}
