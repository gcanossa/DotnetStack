using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public class DropEventArgs
{
  public IEnumerable<DroppedItem> Files { get; set; } = [];

  public class DroppedItem
  {
    public required string ContentType { get; set; }
    public required string Name { get; set; }
    public required string FileId { get; set; }
  }
}


public class DocumentDropEventSource : DocumentEventSourceBase
{

  [DynamicDependency(nameof(Drop))]
  public DocumentDropEventSource(IJSObjectReference module) : base(module)
  {
  }

  public event EventHandler<DropEventArgs>? Drop;

  [JSInvokable]
  public Task OnDrop(DropEventArgs args)
  {
    Drop?.Invoke(this, args);

    return Task.CompletedTask;
  }

  public async ValueTask<IJSStreamReference?> ReadFile(string fileId, bool consume = false)
  {
    var data = await _module.InvokeAsync<IJSStreamReference?>("readFile", fileId, consume);

    return data;
  }

  public async ValueTask RemoveFile(string fileId)
  {
    await _module.InvokeVoidAsync("removeFile", fileId);
  }
}

public class DocumentDropEventService : DocumentEventServiceBase<DocumentDropEventSource>
{

  public DocumentDropEventService(IJSRuntime jsRuntime) : base(jsRuntime)
  {
  }

  protected override DocumentDropEventSource CreateEventSource(IJSObjectReference module)
  {
    return new DocumentDropEventSource(module);
  }
}
