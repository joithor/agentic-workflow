using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.GetSetup;

namespace Aw.Sindri.Host.Commands;

/// <summary>The setup list as 'sindri setup' prints it: one line per step with its plain title, its state and what is true now, then how many are done.</summary>
internal static class SetupText
{
    internal static string Render(GetSetupResponse setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var text = new StringBuilder();
        foreach (var step in setup.Steps)
        {
            _ = text.Append(State(step.State).PadRight(9)).Append(' ').Append(step.Title).Append(": ").Append(step.Detail);
            if (step.Progress is { } progress && step.State != SetupState.Done)
            {
                _ = text.Append(string.Create(CultureInfo.InvariantCulture, $" ({progress.Done} of {progress.Total})"));
            }

            _ = text.Append('\n');
        }

        var done = setup.Steps.Count(static s => s.State == SetupState.Done);
        return text.Append(string.Create(CultureInfo.InvariantCulture, $"{done} of {setup.Steps.Count} steps done.\n")).ToString();
    }

    /// <summary>The state as the API writes it.</summary>
    internal static string State(SetupState state) => state switch
    {
        SetupState.Todo => "todo",
        SetupState.Running => "running",
        SetupState.Done => "done",
        SetupState.Failed => "failed",
        SetupState.NeedsYou => "needs-you",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Not a setup state"),
    };
}
