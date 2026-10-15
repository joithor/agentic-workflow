using Aw.Meta.Core.Owner;

namespace Aw.Meta.Features.Owner;

/// <summary>The ListOwner response.</summary>
/// <param name="Questions">The questions in number order.</param>
public sealed record ListOwnerResponse(IReadOnlyList<OwnerQuestion> Questions);
