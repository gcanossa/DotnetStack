using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public class HtmlAttrSetterService(IJSRuntime jsRuntime) : IAsyncDisposable
{
  private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
      "import", $"./_content/GKit.BlazorExt/{nameof(HtmlAttrSetterService)}.js").AsTask());

  public async ValueTask SetAttributes(ElementReference element, IDictionary<string, object> attributes, string? selector = null)
  {
    var module = await moduleTask.Value;
    await module.InvokeVoidAsync("setAttributes", element, attributes, selector);
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
