using Aw.Meta.Core.Owner;

namespace Aw.Meta.Features.OwnerAnswer;

/// <summary>The AnswerOwner response.</summary>
/// <param name="Question">The answered question.</param>
/// <param name="Relay">The text to send the asking lane, which opens a relay obligation until it is sent.</param>
/// <param name="LogLine">The line appended to the running log.</param>
public sealed record AnswerOwnerResponse(OwnerQuestion Question, string Relay, string LogLine);
