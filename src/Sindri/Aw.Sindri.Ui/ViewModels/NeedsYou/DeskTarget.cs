using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>An open desk run as the instance serves it: where a report goes, and the command the step serves for completing it.</summary>
/// <param name="InstanceId">The instance's id.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Complete">The step's served complete command; null when none is served.</param>
/// <param name="MergeCard">The pull request card, when the instance lists the fact "fact:prStatus" and it read; null otherwise.</param>
internal sealed record DeskTarget(string InstanceId, string Run, StatusCommand? Complete, MergeCardViewModel? MergeCard = null)
{
    /// <summary>True when the step serves its complete command as protected: only the user's terminal may send it (a merge), so the app shows the served command and sends nothing.</summary>
    internal bool IsTerminalOnly => Complete is { } command && string.Equals(command.Class, "protected", StringComparison.Ordinal);
}
