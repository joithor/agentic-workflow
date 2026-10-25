namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>Unwinds a deep walk when a guard trips; the parser turns it into a skip, never an error. Private to this namespace.</summary>
internal sealed class ParseLimitExceededException : Exception
{
    public ParseLimitExceededException()
        : base(SkipReasons.TooDeep)
    {
    }

    public ParseLimitExceededException(string reason)
        : base(reason)
    {
    }

    public ParseLimitExceededException(string reason, Exception innerException)
        : base(reason, innerException)
    {
    }

    /// <summary>The skip word.</summary>
    public string Reason => Message;
}
