namespace Aw.Meta.Features.OwnerAsk;

/// <summary>The AskOwner request: queue a question of the meta's own for the owner.</summary>
/// <param name="Text">The question.</param>
public sealed record AskOwnerRequest(string Text);
