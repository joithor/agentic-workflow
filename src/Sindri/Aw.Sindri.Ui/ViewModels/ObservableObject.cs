using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>Property-change notification for the view models (plain C#, no Avalonia types).</summary>
internal abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Sets a field and raises <see cref="PropertyChanged"/> when the value changed.</summary>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="name">The property's name.</param>
    /// <typeparam name="T">The value type.</typeparam>
    protected void Set<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnChanged(name);
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for a computed property.</summary>
    /// <param name="name">The property's name.</param>
    protected void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
