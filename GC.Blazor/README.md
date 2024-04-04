# GC.Blazor

The library provides a set of services and utility components to ease Blazor development.

## Usage

Add the services to the DI:

```cs
builder.Services.AddGCBlazorServices();
```

## DisposableHandle

The type allows to narrow the type of an object implementing _IDisposable/IAsyncDisposable_ in order to create simple disposable handles without leaking unwanted access.

```cs
var hdl = new DisposableHandle((IDisposable)new Timer(p => {}, null, 1000, 1000));

//...

hdl.Dispose();
```

## DisposableScope

Component to help with the disposal of disposable item at the end of a component lifecycle.

```razor
<DisposableScope>
  <MyComp />
</DisposableScope>
```

Then in \<MyComp\>

```razor
@code{
  [CascadingParameter]
  public DisposableScope Scope { get; set; }

  protected override void OnInitialized()
  {
    Scope.AddDisposable(...);
  }
}
```

## TimerService

Service mimicking _setInterval_ and _setTimeout_ Web Apis.

## TimedScope

Component which provides a scope for a timer in order to produce synchronized rerenders or notifications.

```razor
<TimedScope Period="@TimeSpan.FromSeconds(1)">
  <CurrentTime Now="@DateTime.Now" />
</TimedScope>
```

## FullscreenService

Service which allows to request fullscreen for the document or for a given css selector.

## DownloadFileService

Service which allows to cause a download of a Stream as a file.

## DocumentEventService

Service which allows to subscribe to event of the _document_ object.

## ClipboardService

Service which allows to copy text to clipboard.
