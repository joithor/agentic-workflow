namespace Aw.Sindri.Features.Sessions;

/// <summary>How one read of a source ended.</summary>
internal enum SourceOutcome
{
    Done,
    TimedOut,
    Failed,
}
