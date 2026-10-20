using System.Globalization;

namespace Aw.Sindri.Core.Checks;

/// <summary>A planned command and how its run ended.</summary>
/// <param name="Planned">What ran.</param>
/// <param name="Run">How it ended.</param>
public sealed record CheckResult(PlannedCommand Planned, CheckRun Run)
{
    /// <summary>The command's name.</summary>
    public string Name => Planned.Command.Name;

    /// <summary>A sentence for a finding: 'typecheck exited 2' or 'unit ran past its timeout'.</summary>
    public string Summary => Run.TimedOut ? $"{Name} ran past its timeout" : string.Create(CultureInfo.InvariantCulture, $"{Name} exited {Run.ExitCode}");
}
