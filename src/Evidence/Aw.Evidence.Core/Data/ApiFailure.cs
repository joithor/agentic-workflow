using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>One assertion that did not hold.</summary>
/// <param name="Step">The step's 1-based index.</param>
/// <param name="Location">The JSON Pointer asserted on; null for a status, exit code or golden check.</param>
/// <param name="Expected">What was expected (text, capped).</param>
/// <param name="Actual">What was found (text, capped; 'missing' for an absent pointer).</param>
public sealed record ApiFailure(int Step, [property: JsonPropertyName("pointer")] string? Location, string Expected, string Actual)
{
    /// <summary>The failure as one clause for a headline: '/items/0/title is "a", expected "b"', 'status 500, expected 201'.</summary>
    /// <returns>The clause.</returns>
    public string Describe()
    {
        if (Location is not null)
        {
            return $"{Location} is {Actual}, expected {Expected}";
        }

        const string golden = "the contents of ";
        if (Expected.StartsWith(golden, StringComparison.Ordinal))
        {
            return "output differs from " + Expected[golden.Length..];
        }

        var space = Expected.LastIndexOf(' ');
        return $"{Actual}, expected {(space < 0 ? Expected : Expected[(space + 1)..])}";
    }
}
