namespace Aw.Sindri.Features.Scoping;

/// <summary>Build the stop view (scoping spec §8.4) from the facts a caller holds; 'sindri scoping show --facts', and the server's read model over an instance's facts.</summary>
/// <param name="Facts">Each fact's JSON text by key without the 'fact:' prefix; a missing key makes the sections that need it 'not yet'.</param>
public sealed record ScopingShowRequest(IReadOnlyDictionary<string, string> Facts);
