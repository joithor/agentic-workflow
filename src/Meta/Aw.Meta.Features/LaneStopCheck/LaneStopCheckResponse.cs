using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.LaneStopCheck;

/// <summary>The LaneStopCheck response.</summary>
/// <param name="Blocked">Whether the stop is blocked: a reply is owed and the loop guard hasn't let it through.</param>
/// <param name="Owed">The messages the lane owes a reply to.</param>
/// <param name="Warning">A one-line warning when the guard let the stop through with a reply owed.</param>
public sealed record LaneStopCheckResponse(bool Blocked, IReadOnlyList<LaneMessage> Owed, string? Warning);
