using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public class PasteEventArgs
{
  public IEnumerable<PastedItem> Files { get; set; } = [];

  public class PastedItem
  {
    public required string Type { get; set; }
    public required string Name { get; set; }
    public required string FileId { get; set; }
  }
}


public class DocumentPasteEventSource : DocumentEventSourceBase
{

  [DynamicDependency(nameof(Paste))]
  public DocumentPasteEventSource(IJSObjectReference module) : base(module)
  {
  }

  public event EventHandler<PasteEventArgs>? Paste;

  [JSInvokable]
  public Task OnPaste(PasteEventArgs args)
  {
    Paste?.Invoke(this, args);

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

public class DocumentPasteEventService : DocumentEventServiceBase<DocumentPasteEventSource>
{

  public DocumentPasteEventService(IJSRuntime jsRuntime) : base(jsRuntime)
  {
  }

  protected override DocumentPasteEventSource CreateEventSource(IJSObjectReference module)
  {
    return new DocumentPasteEventSource(module);
  }
}
