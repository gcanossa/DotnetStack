using Bunit;
using GKit.BlazorExt;
using GKit.Tests.Blazor.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace GKit.Tests.Blazor;

public class DbContextProviderTests : TestContext, IDisposable
{
  private readonly GridFixture _fixture = new();

  /// <summary>A child that can only render if the provider cascaded itself down.</summary>
  private sealed class Consumer : ComponentBase
  {
    [CascadingParameter] public DbContextProvider<CatalogContext>? Provider { get; set; }

    public static int Renders;

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
      Renders++;

      // Dereferenced deliberately: the child used to be rendered before OnInitializedAsync had
      // created the context, so this was a null reference on the first pass.
      builder.AddContent(0, Provider!.Context!.GetType().Name);
    }
  }

  private sealed class FakeFactory(GridFixture fixture) : IDbContextFactory<CatalogContext>
  {
    public CatalogContext CreateDbContext() => fixture.NewContext();
  }

  public DbContextProviderTests()
  {
    Services.AddSingleton<IDbContextFactory<CatalogContext>>(new FakeFactory(_fixture));
    Consumer.Renders = 0;
  }

  public new void Dispose()
  {
    _fixture.Dispose();
    base.Dispose();
  }

  [Fact]
  public void The_provider_cascades_itself_to_its_children()
  {
    // DbContextProvider inherits DbContextFactoryProvider but replaced its markup with a bare
    // @ChildContent, discarding the <CascadingValue Value="this"> the base emitted — so a child
    // declaring [CascadingParameter] DbContextProvider<T> never received one.
    var component = RenderComponent<DbContextProvider<CatalogContext>>(p => p
      .AddChildContent<Consumer>());

    Assert.Contains(nameof(CatalogContext), component.Markup);
  }

  [Fact]
  public void Children_are_not_rendered_before_the_context_exists()
  {
    var component = RenderComponent<DbContextProvider<CatalogContext>>(p => p
      .AddChildContent<Consumer>());

    Assert.NotNull(component.Instance.Context);
    // Every render the child saw had a usable context, otherwise it would have thrown.
    Assert.True(Consumer.Renders > 0);
  }

  [Fact]
  public async Task Disposing_the_provider_disposes_the_context()
  {
    var component = RenderComponent<DbContextProvider<CatalogContext>>(p => p
      .AddChildContent<Consumer>());

    await component.Instance.DisposeAsync();

    Assert.Null(component.Instance.Context);
  }
}

public class DocumentEventSourceTests : TestContext
{
  private sealed class TestSource(IJSObjectReference module) : DocumentEventSourceBase(module);

  [Fact]
  public async Task Disconnect_receives_the_dotnet_object_reference_it_connected_with()
  {
    // `disconnect` used to be passed `this`, which the interop layer serialises as JSON: the JS
    // side could not match it to the listener registered by `connect`, so every navigation
    // leaked a document-level handler plus the .NET object reference.
    var module = JSInterop.SetupModule("./_content/GKit.BlazorExt/DocumentKeyboardEventService.js");
    module.SetupVoid("connect", _ => true).SetVoidResult();
    var disconnect = module.SetupVoid("disconnect", _ => true);
    disconnect.SetVoidResult();

    var service = new DocumentKeyboardEventService(JSInterop.JSRuntime);
    var source = await service.Connect();

    await source.DisposeAsync();

    var call = JSInterop.Invocations.Single(i => i.Identifier == "disconnect");
    var argument = call.Arguments.Single();

    Assert.NotNull(argument);
    Assert.IsAssignableFrom<DotNetObjectReference<DocumentEventSourceBase>>(argument);
  }

  [Fact]
  public async Task Disposing_twice_does_not_disconnect_twice()
  {
    var module = JSInterop.SetupModule("./_content/GKit.BlazorExt/DocumentKeyboardEventService.js");
    module.SetupVoid("connect", _ => true).SetVoidResult();
    module.SetupVoid("disconnect", _ => true).SetVoidResult();

    var service = new DocumentKeyboardEventService(JSInterop.JSRuntime);
    var source = await service.Connect();

    await source.DisposeAsync();
    await source.DisposeAsync();

    Assert.Single(JSInterop.Invocations.Where(i => i.Identifier == "disconnect"));
  }
}

public class TimerServiceTests
{
  [Fact]
  public async Task A_throwing_async_callback_is_surfaced_rather_than_swallowed()
  {
    // The returned Task used to be discarded, making a faulting callback an unobserved
    // exception: nothing ran, nothing reported.
    var service = new TimerService();
    var observed = new TaskCompletionSource<Exception>();

    service.OnCallbackError += e => observed.TrySetResult(e);

    using var _ = service.SetIntervalAsync(
      () => throw new InvalidOperationException("boom"), TimeSpan.FromMilliseconds(10));

    var error = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.Equal("boom", error.Message);
  }

  [Fact]
  public async Task A_throwing_sync_callback_is_surfaced_rather_than_swallowed()
  {
    var service = new TimerService();
    var observed = new TaskCompletionSource<Exception>();

    service.OnCallbackError += e => observed.TrySetResult(e);

    using var _ = service.SetInterval(
      () => throw new InvalidOperationException("boom"), TimeSpan.FromMilliseconds(10));

    var error = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.Equal("boom", error.Message);
  }

  [Fact]
  public async Task An_interval_callback_runs_repeatedly()
  {
    var service = new TimerService();
    var ticks = 0;
    var third = new TaskCompletionSource();

    using var _ = service.SetInterval(() =>
    {
      if (Interlocked.Increment(ref ticks) >= 3) third.TrySetResult();
    }, TimeSpan.FromMilliseconds(10));

    await third.Task.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.True(ticks >= 3);
  }

  [Fact]
  public void A_period_beyond_int_MaxValue_milliseconds_is_accepted()
  {
    // The old (int) casts of TotalMilliseconds overflowed past ~24.8 days.
    var service = new TimerService();

    var ex = Record.Exception(() => service.SetInterval(() => { }, TimeSpan.FromDays(30)).Dispose());

    Assert.Null(ex);
  }
}
