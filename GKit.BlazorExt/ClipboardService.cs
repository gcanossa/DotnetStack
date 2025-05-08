using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public class ClipboardService(IJSRuntime jsRuntime)
{
  private readonly IJSRuntime _jsRuntime = jsRuntime;

  public event EventHandler<string> TextSet = default!;

  protected void OnTextSet(string text)
  {
    TextSet?.Invoke(this, text);
  }

  public async Task SetText(string text)
  {
    await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
    OnTextSet(text);
  }
}