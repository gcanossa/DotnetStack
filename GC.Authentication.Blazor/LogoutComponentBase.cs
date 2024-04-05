using Microsoft.AspNetCore.Components;

namespace GC.Authentication.Blazor;

public abstract class LogoutComponentBase : ComponentBase
{
  protected readonly NavigationManager NavigationManager;

  [SupplyParameterFromQuery]
  public string ReturnUrl { get; set; } = "/";
  public LogoutComponentBase(NavigationManager navigationManager)
  {
    NavigationManager = navigationManager;
  }

  protected override async Task OnInitializedAsync()
  {
    await SignOutAsync();
    NavigationManager.NavigateTo(ReturnUrl, forceLoad: true);
  }

  protected abstract Task SignOutAsync();

}