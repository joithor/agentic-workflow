using System.ComponentModel;

namespace Aw.Architecture.Tests.Fixtures.Ui.Desktop;

// ARCH-UI-001 red: a view model in a head.
internal sealed class HeadViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Raise)));
}
