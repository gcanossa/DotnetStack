using Microsoft.AspNetCore.Components;

namespace GC.Authentication.Blazor;

public abstract class LogoutComponentBase : ComponentBase
{
  [Inject]
  protected NavigationManager NavigationManager { get; set; }

  [SupplyParameterFromQuery]
  public string ReturnUrl { get; set; } = "/";

  protected override async Task OnInitializedAsync()
  {
    await SignOutAsync();
    NavigationManager.NavigateTo(ReturnUrl, forceLoad: true);
  }

  protected abstract Task SignOutAsync();

}