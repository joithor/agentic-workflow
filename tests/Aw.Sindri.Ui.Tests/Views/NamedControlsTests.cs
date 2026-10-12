using System.Reflection;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// Every view's x:Name fields are set once it is constructed: a view loads its XAML through the generated
/// InitializeComponent, which binds them, never through AvaloniaXamlLoader.Load(this), which leaves them null.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class NamedControlsTests
{
    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static TheoryData<string> Views => ["SettingsView", "CommandPalette", "ShortcutsSheet", "Shell/ShellWindow", "Shell/TopBar", "Shell/HomePage", "Shell/HealthPage", "Shell/PlaceholderPage"];

    private static Control Construct(string view) => view switch
    {
        "SettingsView" => new SettingsView(),
        "CommandPalette" => new CommandPalette(),
        "ShortcutsSheet" => new ShortcutsSheet(),
        "Shell/ShellWindow" => new ShellWindow(),
        "Shell/TopBar" => new TopBar(),
        "Shell/HomePage" => new HomePage(),
        "Shell/HealthPage" => new HealthPage(),
        "Shell/PlaceholderPage" => new PlaceholderPage(),
        _ => throw new ArgumentOutOfRangeException(nameof(view), view, message: null),
    };

    [Theory]
    [MemberData(nameof(Views))]
    public async Task Every_named_control_is_set_after_construction(string view)
    {
        var names = XDocument.Load(Path.Combine(UiSource.Library, "Views", view + ".axaml"))
            .Descendants().Select(e => (string?)e.Attribute(_x + "Name")).OfType<string>().ToList();

        var unset = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NamedControlsTests).Assembly)
            .Dispatch(() =>
            {
                var control = Construct(view);
                return names.Where(n => control.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(control) is null).ToList();
            }, TestContext.Current.CancellationToken);

        Assert.Empty(unset);
    }
}
