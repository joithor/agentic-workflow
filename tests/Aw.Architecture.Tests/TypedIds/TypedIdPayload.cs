namespace Aw.Architecture.Tests.TypedIds;

// A copy of the example in tests/Aw.Kernel.Tests/TypedIds, as the guard tests' compliant case (that project can't be referenced).
/// <summary>
/// A record carrying one id of each kind, as a package's state file would. The JSON profile requires every
/// constructor parameter (RespectRequiredConstructorParameters), so a missing id is an error, never an
/// uninitialized value object (TypedIdGuardTests checks every id property in this namespace is covered).
/// </summary>
internal sealed record TypedIdPayload(ItemId Item, RunId Run, TokenBudget Budget, string Note);
