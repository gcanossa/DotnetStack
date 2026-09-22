namespace GKit.UI.Data;

/// <summary>
/// The submit and cancel flow of an edit dialog, independent of the form technology behind
/// <see cref="IUiFormHandle"/>.
/// </summary>
/// <remarks>
/// The hook order is deliberate and load-bearing: a form gets a chance to normalise its model
/// before validation runs, to react to the verdict, and to veto submission after validating.
/// </remarks>
public sealed class EditEntityDialogEngine<T> where T : class
{
  public async Task SubmitAsync(IUiFormHandle form, IEditEntityForm<T>? formInstance, IUiDialogHost host, T model)
  {
    await (formInstance?.OnBeforeValidationAsync() ?? Task.CompletedTask);

    var valid = await form.ValidateAsync();

    await (formInstance?.OnAfterValidationAsync(valid) ?? Task.CompletedTask);

    if (!valid)
      return;

    var shouldSubmit = await (formInstance?.OnBeforeSubmitAsync() ?? Task.FromResult(true));

    if (shouldSubmit)
      host.Close(model);
  }

  public async Task CancelAsync(IUiFormHandle form, IUiDialogHost host)
  {
    await form.ResetAsync();
    host.Cancel();
  }
}
