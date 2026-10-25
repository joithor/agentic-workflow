namespace Aw.Sindri.Core.Commands;

/// <summary>
/// A command whose action class depends on its arguments (API spec §2.1, UI spec E-12). Its catalog row is
/// '&lt;Command&gt;:&lt;Variant&gt;', fail-closed: protected fields override the variant, and an unknown variant is protected.
/// </summary>
public interface IClassifiedCommand
{
    /// <summary>The variant, for example 'answer-text': the row '&lt;Command&gt;:&lt;Variant&gt;' gives the class. Null selects the command's own row, the one with no variant.</summary>
    public string? Variant { get; }

    /// <summary>True when the request carries a protected field (grant, grantTokens, accept): it is then classified protected.</summary>
    public bool CarriesProtectedFields { get; }
}
