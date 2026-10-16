using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The head's tray on the UI thread of the headless app: each call is dispatched there, as the app calls it.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class AvaloniaTrayContractTests : TrayContract
{
    protected override ITray Create() => new OnUiThread(OnUi(() => new AvaloniaTray()));

    protected override IReadOnlyList<string> MenuLabels(ITray tray) =>
        OnUi(() => ((OnUiThread)tray).Unwrap().Menu!.Items.OfType<NativeMenuItem>().Where(i => i is not NativeMenuItemSeparator).Select(i => i.Header!).ToList());

    protected override void Pick(ITray tray, int index) => OnUi(() =>
    {
        ((OnUiThread)tray).Unwrap().Menu!.Items.OfType<NativeMenuItem>().Where(i => i is not NativeMenuItemSeparator).ElementAt(index).Command!.Execute(parameter: null);
        return true;
    });

    private static T OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaTrayContractTests).Assembly)
            .Dispatch(body, TestContext.Current.CancellationToken).GetAwaiter().GetResult();

    private sealed class OnUiThread(AvaloniaTray tray) : ITray
    {
        public AvaloniaTray Unwrap() => tray;

        public TrayView? Shown => OnUi(() => tray.Shown);

        public event EventHandler? OpenRequested
        {
            add => tray.OpenRequested += value;
            remove => tray.OpenRequested -= value;
        }

        public event EventHandler<TrayRowEventArgs>? RowRequested
        {
            add => tray.RowRequested += value;
            remove => tray.RowRequested -= value;
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
