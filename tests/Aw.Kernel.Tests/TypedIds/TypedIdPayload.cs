namespace Aw.Kernel.Tests.TypedIds;

/// <summary>
/// A record carrying one id of each kind, as a package's state file would. The JSON profile requires every
/// constructor parameter (RespectRequiredConstructorParameters), so a missing id is an error, never an
/// uninitialized value object (TypedIdGuard, in tests/Aw.Architecture.Tests, checks this shape for every registered assembly).
/// </summary>
internal sealed record TypedIdPayload(ItemId Item, RunId Run, TokenBudget Budget, string Note);
