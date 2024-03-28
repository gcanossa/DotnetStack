using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace GC.Utils.UI.Services;

public class DocumentEventListener
{
  public event EventHandler<KeyboardEventArgs>? KeyUp;
  public event EventHandler<KeyboardEventArgs>? KeyDown;
  public event EventHandler<KeyboardEventArgs>? KeyPress;

  [DynamicDependency(nameof(KeyUp))]
  [DynamicDependency(nameof(KeyDown))]
  [DynamicDependency(nameof(KeyPress))]
  public DocumentEventListener()
  {
  }

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