using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Aw.UiDriver.Avalonia.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>The headless app the driver tests run in: Skia drawing on, so a screenshot has pixels, and no theme, so nothing but the test's own controls is on screen.</summary>
public static class TestAppBuilder
{
    /// <summary>Builds the app for the headless session.</summary>
    /// <returns>The builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>A command that counts how often it ran.</summary>
internal sealed class CountingCommand : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public int Runs { get; private set; }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => Runs++;
}

/// <summary>The window the tests drive: a form with a button, a text box, a toggle, a disabled button, a scrolling list and a key listener.</summary>
internal sealed class TestScreen
{
    internal TestScreen()
    {
        Greeting = new TextBlock { Text = "Hello, Ada" };
        AutomationProperties.SetAutomationId(Greeting, "greeting");
        Save = new Button { Content = "Save", Command = Saved };
        AutomationProperties.SetAutomationId(Save, "save");
        Off = new Button { Content = "Off", IsEnabled = false };
        AutomationProperties.SetAutomationId(Off, "off");
        NameBox = new TextBox { Text = "Ada" };
        AutomationProperties.SetAutomationId(NameBox, "name");
        Dark = new ToggleButton { Content = "Dark" };
        AutomationProperties.SetAutomationId(Dark, "dark");
        Listener = new Border { Focusable = true, Width = 20, Height = 20 };
        AutomationProperties.SetAutomationId(Listener, "listener");
        Listener.KeyDown += (_, e) => Keys.Add($"{e.KeyModifiers}+{e.Key}");
        var rows = new StackPanel();
        for (var i = 0; i < 40; i++)
        {
            rows.Children.Add(new TextBlock { Text = $"row {i}", Height = 20 });
        }

        List = new ScrollViewer { Height = 100, Content = rows };
        AutomationProperties.SetAutomationId(List, "list");
        var form = new StackPanel { Children = { Greeting, Save, Off, NameBox, Dark, Listener, List } };
        Window = new Window { Width = 400, Height = 300, Title = "Test screen", Content = form };
        Window.Show();
    }

    internal CountingCommand Saved { get; } = new();

    internal List<string> Keys { get; } = [];

    internal Window Window { get; }

    internal TextBlock Greeting { get; }

    internal Button Save { get; }

    internal Button Off { get; }

    internal TextBox NameBox { get; }

    internal ToggleButton Dark { get; }

    internal Border Listener { get; }

    internal ScrollViewer List { get; }

    /// <summary>Builds the screen on the headless UI thread.</summary>
    /// <returns>The screen.</returns>
    internal static Task<TestScreen> OpenAsync() =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestScreen).Assembly).Dispatch(static () => new TestScreen(), TestContext.Current.CancellationToken);

    /// <summary>Runs work on the headless UI thread.</summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="work">The work.</param>
    /// <returns>Its result.</returns>
    internal static Task<T> OnUi<T>(Func<T> work) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestScreen).Assembly).Dispatch(work, TestContext.Current.CancellationToken);
}
