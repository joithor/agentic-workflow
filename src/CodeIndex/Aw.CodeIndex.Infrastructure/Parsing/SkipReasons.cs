using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>The words a skipped file carries in <see cref="ParseOutcome.Skip"/> (spec §9 CI12, §5.1 ceilings).</summary>
internal static class SkipReasons
{
    /// <summary>The syntax nests deeper than the cap, or the stack ran short.</summary>
    public const string TooDeep = "too-deep";

    /// <summary>The file holds more symbols than the ceiling.</summary>
    public const string TooManySymbols = "too-many-symbols";

    /// <summary>The file holds more call sites than the ceiling.</summary>
    public const string TooManyCallSites = "too-many-call-sites";

    /// <summary>The parse ran past its time limit.</summary>
    public const string Timeout = "timeout";

    /// <summary>The parser failed on this file in a way it did not expect; the file is left unparsed rather than failing the build.</summary>
    public const string ParserFailed = "parser-failed";
}
