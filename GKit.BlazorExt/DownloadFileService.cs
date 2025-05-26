using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public class DownloadFileService(IJSRuntime jsRuntime) : IAsyncDisposable
{
  private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
      "import", $"./_content/GKit.BlazorExt/{nameof(DownloadFileService)}.js").AsTask());

  public async Task DownloadFileFromStream(Stream source, string fileName)
  {
    var module = await moduleTask.Value;

    using var streamRef = new DotNetStreamReference(stream: source);

    await module.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
  }

  public async Task<string> CreateObjectURLFromStream(Stream source, string? contentType = null)
  {
    var module = await moduleTask.Value;

    using var streamRef = new DotNetStreamReference(stream: source);

    return await module.InvokeAsync<string>("createObjectURLFromStream", streamRef, contentType);
  }

  public async Task RevokeObjectURL(string url)
  {
    var module = await moduleTask.Value;

    await module.InvokeVoidAsync("revokeObjectURL", url);
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