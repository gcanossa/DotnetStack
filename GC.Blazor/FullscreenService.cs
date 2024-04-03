using Microsoft.JSInterop;

namespace GC.Blazor;

public class FullscreenService(IJSRuntime jsRuntime) : IAsyncDisposable
{
  private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
      "import", $"./_content/GC.Blazor/{nameof(FullscreenService)}.js").AsTask());

  public async ValueTask OpenFullscreen(string? selector = null)
  {
    var module = await moduleTask.Value;
    await module.InvokeVoidAsync("openFullscreen", selector);
  }

  public async ValueTask CloseFullscreen(string? selector = null)
  {
    var module = await moduleTask.Value;
    await module.InvokeVoidAsync("closeFullscreen", selector);
  }

  public async ValueTask DisposeAsync()
  {
    if (moduleTask.IsValueCreated)
    {
      var module = await moduleTask.Value;
      await module.DisposeAsync();
    }
  }
}
