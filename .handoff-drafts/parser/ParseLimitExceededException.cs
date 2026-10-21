namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>Unwinds a deep walk when a guard trips; the parser turns it into a skip, never an error. Private to this namespace.</summary>
internal sealed class ParseLimitExceededException : Exception
{
    public ParseLimitExceededException(string reason)
        : base(reason) => Reason = reason;

    /// <summary>The skip word.</summary>
    public string Reason { get; }
}
