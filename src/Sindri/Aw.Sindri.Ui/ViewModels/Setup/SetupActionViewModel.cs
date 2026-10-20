using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>
/// The one action a step offers (setup-once spec §7.4): a button the app presses itself when it can do the action, otherwise the terminal command to copy. The command is the
/// catalog's own when it lists the action, else the spec's form; the app never runs a protected action's command for the person.
/// </summary>
internal sealed class SetupActionViewModel
{
    private readonly Action<string>? _open;

    internal SetupActionViewModel(SetupStepView step, bool inApp, CatalogViewModel catalog, TimeProvider clock, Action<string>? open)
    {
        var action = step.Action ?? string.Empty;
        (Action, IsInApp, _open) = (action, inApp, open);
        Done = string.Equals(step.State, "done", StringComparison.Ordinal);
        Label = Labels.SetupActionLabel(action, Done);
        Name = Label + ", " + step.Title;
        if (!inApp)
        {
            var cli = catalog.Row(action) is { Cli: { Length: > 0 } served } ? served : Labels.SetupCli(action);
            Cli = cli.Length == 0 ? null : new ControlViewModel(new CommandRowView(action, "desk", [], Terminal: false, Method: null, Route: null, cli, "cli-only:setup", Destructive: false, ReadsViews: false, Shortcut: null), send: null, catalog.Clipboard, clock);
        }
    }

    /// <summary>The terminal copy of a protected action, or null.</summary>
    public Components.ProtectedActionViewModel? Protected { get; private set; }

    /// <summary>True when the action is protected and shows as terminal copy.</summary>
    public bool HasProtected => Protected is not null;

    /// <summary>The catalog action id.</summary>
    public string Action { get; }

    /// <summary>The button's words ("Approve all workflows"; "Add another repo" once the step is done).</summary>
    public string Label { get; }

    /// <summary>The button's accessible name: its words first, then the step.</summary>
    public string Name { get; }

    /// <summary>True when the app does it itself (a button); false when the person runs the command.</summary>
    public bool IsInApp { get; }

    /// <summary>The terminal command with Copy, when the app does not do it itself; else null.</summary>
    public ControlViewModel? Cli { get; }

    /// <summary>True when the command shows (the view's switch).</summary>
    public bool HasCli => Cli is not null;

    /// <summary>The line over the command.</summary>
    public string CliHint { get; } = Labels.SetupCliHint;

    /// <summary>True when the step it belongs to is done.</summary>
    internal bool Done { get; }

    /// <summary>Whether this is still the action for <paramref name="step"/>: the same action, drawn the same way, with the same words.</summary>
    /// <param name="step">The step as served now.</param>
    /// <param name="inApp">Whether the app does the action itself now.</param>
    /// <returns>True when nothing about it changed.</returns>
    internal bool Matches(SetupStepView step, bool inApp) =>
        string.Equals(Action, step.Action, StringComparison.Ordinal) && IsInApp == inApp && Done == string.Equals(step.State, "done", StringComparison.Ordinal) && string.Equals(Name, Label + ", " + step.Title, StringComparison.Ordinal);

    /// <summary>Presses the button: asks the shell to open the action.</summary>
    internal void Open()
    {
        if (IsInApp)
        {
            _open?.Invoke(Action);
        }
    }
}
