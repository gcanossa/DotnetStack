using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public class ClipboardEventArgs
{
  public IEnumerable<ClipboardItem> Files { get; set; } = [];

  public class ClipboardItem
  {
    public required string ContentType { get; set; }
    public required string Name { get; set; }
    public required string FileId { get; set; }
  }
}

public class ClipboardService(IJSRuntime jsRuntime)
{
  protected readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
      "import", $"./_content/GKit.BlazorExt/{nameof(ClipboardService)}.js").AsTask());

  public async Task WriteText(string text)
  {
    var module = await moduleTask.Value;
    await module.InvokeVoidAsync("writeText", text);
  }

  public async Task<string> ReadText()
  {
    var module = await moduleTask.Value;
    return await module.InvokeAsync<string>("readText");
  }

  public async Task<ClipboardEventArgs> ReadItems()
  {
    var module = await moduleTask.Value;
    return await module.InvokeAsync<ClipboardEventArgs>("read");
  }

  public async ValueTask<IJSStreamReference?> ReadFile(string fileId, bool consume = false)
  {
    var module = await moduleTask.Value;
    var data = await module.InvokeAsync<IJSStreamReference?>("readFile", fileId, consume);

    return data;
  }
  public async ValueTask RemoveFile(string fileId)
  {
    var module = await moduleTask.Value;
    await module.InvokeVoidAsync("removeFile", fileId);
  }
}