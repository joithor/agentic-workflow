using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The head's tray on the UI thread of the headless app: each call is dispatched there, as the app calls it.</summary>
[Lane(TestLane.Integration)]
public sealed class AvaloniaTrayContractTests : TrayContract
{
    protected override ITray Create() => new OnUiThread(OnUi(() => new AvaloniaTray()));

    private static T OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaTrayContractTests).Assembly)
            .Dispatch(body, TestContext.Current.CancellationToken).GetAwaiter().GetResult();

    private sealed class OnUiThread(AvaloniaTray tray) : ITray
    {
        public TrayView? Shown => OnUi(() => tray.Shown);

        public event EventHandler? OpenRequested
        {
            add => tray.OpenRequested += value;
            remove => tray.OpenRequested -= value;
        }

        public event EventHandler? QuitRequested
        {
            add => tray.QuitRequested += value;
            remove => tray.QuitRequested -= value;
        }

        public void Show(TrayView view) => _ = OnUi(() =>
        {
            tray.Show(view);
            return true;
        });
    }
}
