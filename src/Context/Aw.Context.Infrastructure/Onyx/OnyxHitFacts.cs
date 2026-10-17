namespace Aw.Context.Infrastructure.Onyx;

/// <summary>What a search hit said about a document that its fetch routes don't (title, author, updated time).</summary>
/// <param name="Title">The connector-built title.</param>
/// <param name="Author">The first primary owner, if any.</param>
/// <param name="UpdatedAt">When the source last changed it, if known.</param>
/// <param name="CreatedAt">When the source created it (the connector metadata's 'created_at'), if known.</param>
internal sealed record OnyxHitFacts(string Title, string? Author, DateTimeOffset? UpdatedAt, DateTimeOffset? CreatedAt = null);
