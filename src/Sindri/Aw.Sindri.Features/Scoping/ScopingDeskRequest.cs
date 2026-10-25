namespace Aw.Sindri.Features.Scoping;

/// <summary>Run a block Step's code over the facts a caller holds and return the report a desk 'complete' takes (scoping spec §8.2, 'Desk helper').</summary>
/// <param name="Block">The Step: a full id ('step.scope.check') or its last part ('check').</param>
/// <param name="Facts">Each declared input fact's JSON text by key without the 'fact:' prefix.</param>
public sealed record ScopingDeskRequest(string Block, IReadOnlyDictionary<string, string> Facts);
