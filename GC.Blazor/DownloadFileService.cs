using Microsoft.JSInterop;

namespace GC.Blazor;

public class DownloadFileService(IJSRuntime jsRuntime) : IAsyncDisposable
{
  private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
      "import", $"./_content/GC.Blazor/{nameof(DownloadFileService)}.js").AsTask());

  public async Task DownloadFileFromStream(Stream source, string fileName)
  {
    var module = await moduleTask.Value;

    using var streamRef = new DotNetStreamReference(stream: source);

    await module.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
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