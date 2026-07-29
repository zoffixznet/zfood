using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZFood.Core;

/// <summary>
/// Minimal INotifyPropertyChanged base so UI toolkits can bind directly to core
/// models without this library referencing any of them.
/// </summary>
public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Raise(name);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
