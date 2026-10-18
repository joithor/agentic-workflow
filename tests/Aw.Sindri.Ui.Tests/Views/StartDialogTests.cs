using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The Start form as a modal dialog (U5'): `N` and the buttons open it with focus on the recipe picker, Esc closes it, Ctrl+Enter sends
/// it, a refusal is read inline and takes focus, and every field is named by its visible words. Two key frames (frames/u5p-start-*.png).
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class StartDialogTests : FrameHost
{
    private const string Prefix = "u5p-";

    private static void Frame(Window window, string name) => Frame(window, name, Prefix);

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnWorkflows()
    {
        var (window, shell) = await Connected(width: 1400, height: 900, ready: s => s.Workflows.Groups.Count > 0 && s.Health.Catalog.Controls.Count > 0);
        shell.GoTo(PageId.Workflows);
        Settle(window);
        return (window, shell);
    }

    private static async Task Click(Window window, string name)
    {
        var button = Named<Button>(window, name);
        Assert.True(button.IsEffectivelyEnabled);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
        Settle(window);
    }

    private static async Task Fill(Window window, ShellViewModel shell, string item, string value)
    {
        await Until(window, () => shell.Start.Recipes.Count > 0 && !shell.Start.IsLoading);
        await shell.Start.Choose(shell.Start.Recipes[0], Token);
        shell.Start.ItemId = item;
        shell.Start.Fields[0].Value = value;
        Settle(window);
    }

    [Fact]
    public Task N_and_the_two_buttons_open_the_form_with_focus_on_the_recipe_picker_and_Esc_closes_it() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();

        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None);
        await Until(window, () => shell.Start.IsOpen && Focused(window) == Named<ComboBox>(window, "StartRecipe"));
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Until(window, () => !shell.Start.IsOpen);
        await Click(window, "WorkflowsStart");
        await Until(window, () => shell.Start.IsOpen && Focused(window) == Named<ComboBox>(window, "StartRecipe"));
        shell.Start.Close();
        shell.GoTo(PageId.Home);
        Settle(window);
        await Click(window, "HomeStart");

        await Until(window, () => shell.Start.IsOpen && Focused(window) == Named<ComboBox>(window, "StartRecipe"));
        Assert.Equal("Start workflow", Named<Button>(window, "StartButton").GetVisualDescendants().OfType<TextBlock>().First().Text);
    });

    [Fact]
    public Task Ctrl_Enter_sends_the_form_and_the_instance_frame_opens() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, "the spec");
        Frame(window, "start-filled");

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => !shell.Start.IsOpen && shell.IsInstanceSheet);

        Assert.Equal(Wire(StartGoldens.Request()), Wire(Assert.Single(Server.Started)));
    });

    [Fact]
    public Task A_refusal_is_read_inline_and_takes_focus_and_the_form_stays_pressable() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, "the spec");
        Server.StartAnswer = RouteAnswer.Problem("workflow-instances", "start-missing-input");

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => shell.Start.HasError && Focused(window) == Named<Border>(window, "StartError"));

        Assert.Equal((true, true), (shell.Start.IsOpen, shell.Start.CanStart));
        Assert.Equal(shell.Start.Error, Heard.Name(Named<Border>(window, "StartError")));
        Frame(window, "start-refused");
    });

    // Every field is named by the words it shows, and the card is a window; walked the way an assistive client walks it.
    [Fact]
    public Task Every_field_is_heard_by_the_words_it_shows() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, string.Empty);

        var heard = Heard.Tree(Named<Control>(window, "StartCard")).Select(n => n.Name).ToList();

        Assert.Equal((AutomationControlType.Window, Labels.StartWorkflow), (ControlAutomationPeer.CreatePeerForElement(Named<Control>(window, "StartCard")).GetAutomationControlType(), Heard.Name(Named<Control>(window, "StartCard"))));
        Assert.Contains(Labels.StartRecipe, heard, StringComparer.Ordinal);
        Assert.Contains(Labels.StartItem, heard, StringComparer.Ordinal);
        Assert.All(shell.Start.Fields, f => Assert.Contains(f.Caption, heard, StringComparer.Ordinal));
        Assert.Contains(Labels.Cancel, heard, StringComparer.Ordinal);
        Assert.Contains(Labels.StartWorkflow, heard, StringComparer.Ordinal);
    });

    private static string Wire(Ui.Api.Workflow.StartInstanceRequest request) =>
        System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(request, Ui.Api.SindriApiClient.Json));
}
