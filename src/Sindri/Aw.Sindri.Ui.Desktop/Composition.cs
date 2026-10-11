using Avalonia;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The head's composition, by hand: one adapter per Aw.Sindri.Ui port (control.json on the local disk) handed to the app.
/// There is no container: the app has one view model and one client, and a service locator would sit outside a Host
/// (ARCH-DI-001).
/// </summary>
internal static class Composition
{
    internal static AppBuilder Compose(Func<string, string?> environment, string userHome, TimeProvider clock)
    {
        var records = new FileEndpointRecords(ControlFilePath.From(environment, userHome));
        return AppBuilder.Configure(() => SindriApp.Create(records, clock))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
