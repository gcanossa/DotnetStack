using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace GKit.BlazorExt;

public class CascadingStateValueSource<T>
    : CascadingValueSource<T>, IDisposable where T : INotifyPropertyChanged
{
    private readonly T state;

    public CascadingStateValueSource(T state, bool isFixed = false)
        : base(state, isFixed)
    {
        this.state = state;
        this.state.PropertyChanged += HandlePropertyChanged;
    }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = NotifyChangedAsync();
    }

    public void Dispose()
    {
        state.PropertyChanged -= HandlePropertyChanged;
    }
}