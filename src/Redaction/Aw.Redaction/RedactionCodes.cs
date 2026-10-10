namespace Aw.Redaction;

/// <summary>Error codes Redaction reports (AW-REDACT-NNN). Redaction is BCL-only, so these are plain codes in the
/// Kernel catalogue's format rather than ErrorDefinitions.</summary>
public static class RedactionCodes
{
    /// <summary>A pattern ran past its match timeout, so the text was withheld instead of passed on.</summary>
    public const string MatchTimeout = "AW-REDACT-001";
}
