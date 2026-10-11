using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'command.handled' audit payload (API spec §2.1, §3.4): one row per command attempt that reached the chain. Not public.</summary>
/// <param name="CommandId">The attempt.</param>
/// <param name="Command">The action id it was classified as.</param>
/// <param name="Class">Its action class.</param>
/// <param name="Caller">The caller's actor ('none' without a caller context).</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Code">The error code when it didn't succeed.</param>
public sealed record CommandHandled(CommandId CommandId, ActionId Command, ActionClass Class, string Caller, CommandOutcome Outcome, string? Code);
