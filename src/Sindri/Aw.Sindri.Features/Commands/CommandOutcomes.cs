using Aw.Kernel;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.Commands;

/// <summary>How an attempt's result reads in its audit row (API spec §2.1): SND-AUTH-* refused, an Invalid error invalid, any other error failed.</summary>
internal static class CommandOutcomes
{
    internal static CommandOutcome Of(Error? error) => error switch
    {
        null => CommandOutcome.Accepted,
        { Code: var code } when code.StartsWith("SND-AUTH-", StringComparison.Ordinal) => CommandOutcome.Refused,
        { Kind: ErrorKind.Invalid } => CommandOutcome.Invalid,
        _ => CommandOutcome.Failed,
    };
}
