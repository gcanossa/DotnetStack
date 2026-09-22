using Microsoft.AspNetCore.Components;

namespace GKit.Authentication.Blazor;

public abstract class LoginComponentBase<T> : ComponentBase where T : new()
{
  [Inject]
  protected NavigationManager NavigationManager { get; set; } = null!;

  [SupplyParameterFromForm] public T? Model { get; set; }

  [SupplyParameterFromQuery]
  public string ReturnUrl { get; set; } = "/";

  public bool FailedLoginAttempt { get; protected set; } = false;

  protected async Task LoginUser()
  {
    var result = await SignInAsync(Model ?? new T());

    if (result)
    {
      // ReturnUrl comes straight from the query string; never hand a just-authenticated user
      // to an off-application address.
      NavigationManager.NavigateTo(LocalUrl.EnsureLocal(ReturnUrl));
    }
    else
    {
      FailedLoginAttempt = true;
    }
  }

  protected abstract Task<bool> SignInAsync(T credentials);

}