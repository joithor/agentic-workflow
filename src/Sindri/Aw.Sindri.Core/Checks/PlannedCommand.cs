using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Checks;

/// <summary>A profile command and the arguments the Step adds to it (the red command gets the test ids).</summary>
/// <param name="Command">The command from 'workflow.build.commands'.</param>
/// <param name="ExtraArguments">Appended to its argv.</param>
public sealed record PlannedCommand(BuildCommand Command, IReadOnlyList<string> ExtraArguments);
