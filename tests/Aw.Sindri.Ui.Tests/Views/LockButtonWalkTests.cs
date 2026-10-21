using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.Views.Components;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// Rev 8 critique run 5 P1: a lock is never a dead end. Whatever draws "⌂ ..." is the protected-action button with its
/// popover: no source draws the glyph by hand (XAML or C#), and no realized view of the library draws it outside one.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class LockButtonWalkTests
{
    private const char Lock = '⌂';

    // The files that name the glyph: the button, its label and its view model's doc line.
    private static readonly string[] _owners = ["ProtectedActionButton.axaml", "Labels.Protected.cs", "ProtectedActionViewModel.cs"];

    // The needs-you action bar's terminal button (needs-you spec "Terminal-only"): one press copies the served command and the hint
    // says "Copied", so it is no dead end; it has no popover by design (A copies it too). It is the one other host of the glyph.
    private const string BarTerminalSource = "NeedsYouPage.axaml";
    private const string BarTerminalName = "NeedsYouTerminal";

    [Fact]
    public void No_source_draws_the_lock_glyph_but_the_protected_action()
    {
        var byHand = Directory.EnumerateFiles(UiSource.Library, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".axaml", StringComparison.Ordinal))
                && !f.Split(Path.DirectorySeparatorChar).Any(p => string.Equals(p, "bin", StringComparison.Ordinal) || string.Equals(p, "obj", StringComparison.Ordinal))
                && !_owners.Contains(Path.GetFileName(f), StringComparer.Ordinal)
                && !string.Equals(Path.GetFileName(f), BarTerminalSource, StringComparison.Ordinal)
                && (File.ReadAllText(f).Contains(Lock, StringComparison.Ordinal) || File.ReadAllText(f).Contains("\\u2302", StringComparison.OrdinalIgnoreCase)))
            .Select(Path.GetFileName).ToList();

        Assert.Empty(byHand);
    }

    // The label text is built by Labels.ProtectedLabel, so a view or view model that calls it draws the glyph without the
    // glyph appearing in its own source: only the view model behind the protected-action button may.
    [Fact]
    public void No_source_asks_for_the_protected_label_but_its_view_model()
    {
        var callers = Directory.EnumerateFiles(UiSource.Library, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".axaml", StringComparison.Ordinal))
                && !f.Split(Path.DirectorySeparatorChar).Any(p => string.Equals(p, "bin", StringComparison.Ordinal) || string.Equals(p, "obj", StringComparison.Ordinal))
                && !_owners.Contains(Path.GetFileName(f), StringComparer.Ordinal)
                && File.ReadAllText(f).Contains("ProtectedLabel", StringComparison.Ordinal))
            .Select(Path.GetFileName).ToList();

        Assert.Empty(callers);
    }

    [Theory]
    [MemberData(nameof(NamedControlsTests.Views), MemberType = typeof(NamedControlsTests))]
    public async Task No_realized_view_draws_the_lock_glyph_outside_a_protected_action_button(string view)
    {
        var stray = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(LockButtonWalkTests).Assembly)
            .Dispatch(() =>
            {
                var window = Realize(view);
                try
                {
                    return window.GetVisualDescendants().OfType<TextBlock>()
                        .Where(t => t.Text?.StartsWith(Lock) == true && !t.GetVisualAncestors().OfType<ProtectedActionButton>().Any()
                            && !t.GetVisualAncestors().OfType<Button>().Any(b => string.Equals(b.Name, BarTerminalName, StringComparison.Ordinal)))
                        .Select(t => t.Text).ToList();
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);

        Assert.Empty(stray);
    }

    // The positive half, so the walk above is not vacuous: the one view that draws the lock draws its label, and the
    // popover it declares opens.
    [Fact]
    public async Task The_protected_action_button_draws_the_lock_and_its_popover_opens()
    {
        var (label, opened) = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(LockButtonWalkTests).Assembly)
            .Dispatch(() =>
            {
                var window = Realize("Components/ProtectedActionButton");
                try
                {
                    var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "LockButton");
                    var flyout = button.Flyout!;
                    flyout.ShowAt(button);
                    Dispatcher.UIThread.RunJobs();
                    var open = flyout.IsOpen;
                    flyout.Hide();
                    return (window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => t?.StartsWith(Lock) == true), open);
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);

        Assert.NotNull(label);
        Assert.True(opened);
    }

    // The view in a window, with its fixture data context where one is cheap (the lock button's is a model over a fake clipboard).
    private static Window Realize(string view)
    {
        var control = Construct(view);
        if (control is ProtectedActionButton)
        {
            control.DataContext = new ProtectedActionViewModel(new ProtectedAction("Accept", "sindri decision accept REQ-01JD4", "REQ-01JD4"), new FakeClipboard(), new FakeTimeProvider(A.Noon));
        }

        var window = control as Window ?? new Window { Content = control, Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private static Control Construct(string view) => NamedControlsTests.Build(view);
}
