using System.Text.RegularExpressions;

namespace Aw.Kernel;

/// <summary>Checks an error catalogue: every code well formed and unique.</summary>
public static partial class ErrorCatalogue
{
    /// <summary>Validates <paramref name="definitions"/>.</summary>
    /// <param name="definitions">A package's catalogue.</param>
    /// <returns>Success, or AW-KERNEL-001 for a malformed code, AW-KERNEL-003 for a duplicate.</returns>
    public static Result<Unit> Validate(IEnumerable<ErrorDefinition> definitions)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (!IsWellFormed(definition.Code))
            {
                return KernelErrors.Invalid.With(
                    $"Error code '{definition.Code}' is not AW-AREA-NNN or SND-AREA-NNN (ASCII capitals and digits, three-digit number).");
            }

            if (!seen.Add(definition.Code))
            {
                return KernelErrors.Conflict.With($"Error code '{definition.Code}' is defined more than once.");
            }
        }

        return Results.Ok();
    }

    private static bool IsWellFormed(string code)
    {
        try
        {
            return CodePattern.IsMatch(code);
        }
        catch (RegexMatchTimeoutException)
        {
            // A timeout never passes (spec §4c): the code counts as malformed.
            return false;
        }
    }

    // Explicit ASCII classes and CultureInvariant, with a match timeout (spec §4c, regex parity).
    [GeneratedRegex("^(AW|SND)-[A-Z0-9]+-[0-9]{3}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex CodePattern { get; }
}
