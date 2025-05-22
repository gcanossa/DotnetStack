using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public class DocumentKeyboardEventSource : DocumentEventSourceBase
{
  [DynamicDependency(nameof(KeyUp))]
  [DynamicDependency(nameof(KeyDown))]
  [DynamicDependency(nameof(KeyPress))]
  public DocumentKeyboardEventSource(IJSObjectReference module) : base(module)
  {
  }

  public event EventHandler<KeyboardEventArgs>? KeyUp;
  public event EventHandler<KeyboardEventArgs>? KeyDown;
  public event EventHandler<KeyboardEventArgs>? KeyPress;

  [JSInvokable]
  public Task OnKeyUp(KeyboardEventArgs args)
  {
    KeyUp?.Invoke(this, args);

    return Task.CompletedTask;
  }

  [JSInvokable]
  public Task OnKeyDown(KeyboardEventArgs args)
  {
    KeyDown?.Invoke(this, args);

    return Task.CompletedTask;
  }

  [JSInvokable]
  public Task OnKeyPress(KeyboardEventArgs args)
  {
    KeyPress?.Invoke(this, args);

    return Task.CompletedTask;
  }
}

public class DocumentKeyboardEventService : DocumentEventServiceBase<DocumentKeyboardEventSource>
{

  public DocumentKeyboardEventService(IJSRuntime jsRuntime) : base(jsRuntime)
  {
  }

  protected override DocumentKeyboardEventSource CreateEventSource(IJSObjectReference module)
  {
    return new DocumentKeyboardEventSource(module);
  }
}
