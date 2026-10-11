namespace Aw.Evidence.Features.ParityPut;

/// <summary>The command to run.</summary>
/// <param name="Argv">curl's argv: every signed header verbatim, the bytes untouched.</param>
public sealed record ParityPutResponse(IReadOnlyList<string> Argv);
