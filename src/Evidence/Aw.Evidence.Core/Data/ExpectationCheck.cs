using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aw.Evidence.Core.Data;

/// <summary>Checks an expectation against what a request or a CLI call produced.</summary>
public static class ExpectationCheck
{
    private const string NotJson = "not JSON";
    private const string Missing = "missing";

    /// <summary>Runs every assertion; none stops the others.</summary>
    /// <param name="step">The step's 1-based index.</param>
    /// <param name="expect">The expectation.</param>
    /// <param name="actualCode">The HTTP status or exit code.</param>
    /// <param name="output">The body or stdout.</param>
    /// <param name="goldenText">The golden file's text, when the expectation names one.</param>
    /// <returns>How many assertions there were and which failed (values uncapped and unscrubbed).</returns>
    public static CheckedExpectation Evaluate(int step, Expectation expect, int actualCode, string output, string? goldenText)
    {
        var tally = new Tally();
        var code = actualCode.ToString(CultureInfo.InvariantCulture);
        if (expect.Status is { } status)
        {
            tally.Add(status == actualCode, new ApiFailure(step, Location: null, "status " + status.ToString(CultureInfo.InvariantCulture), "status " + code));
        }
        else if (expect.ExitCode is { } exit)
        {
            tally.Add(exit == actualCode, new ApiFailure(step, Location: null, "exit code " + exit.ToString(CultureInfo.InvariantCulture), "exit code " + code));
        }

        using var document = Parse(output);
        foreach (var check in expect.Pointers ?? [])
        {
            var failure = Check(step, check, document?.RootElement);
            tally.Add(failure is null, failure);
        }

        if (expect.Golden is { } golden)
        {
            tally.Add(GoldenHolds(output, document, goldenText), new ApiFailure(step, Location: null, "the contents of " + golden, output));
        }

        return tally.Result();
    }

    private static ApiFailure? Check(int step, PointerCheck check, JsonElement? root)
    {
        var found = root is { } document ? JsonPointer.Resolve(document, check.Location) : null;
        var actual = root is null ? NotJson : found?.GetRawText() ?? Missing;
        if (check.EqualTo is { } expected)
        {
            return found is { } value && JsonEquality.Equal(value, expected) ? null : new ApiFailure(step, check.Location, expected.GetRawText(), actual);
        }

        return check.Exists is { } exists ? CheckExists(step, check, found is not null, exists, actual) : CheckMatches(step, check, found, actual);
    }

    private static ApiFailure? CheckExists(int step, PointerCheck check, bool present, bool exists, string actual) =>
        present == exists ? null : new ApiFailure(step, check.Location, exists ? "exists" : "absent", actual);

    private static ApiFailure? CheckMatches(int step, PointerCheck check, JsonElement? found, string actual) =>
        MatchHolds(found, check.Matches ?? string.Empty, out var timedOut) ? null : new ApiFailure(step, check.Location, "matches " + check.Matches, timedOut ? "match timed out" : actual);

    private static bool MatchHolds(JsonElement? found, string pattern, out bool timedOut)
    {
        timedOut = false;
        var text = found switch
        {
            { ValueKind: JsonValueKind.String } value => value.GetString(),
            { ValueKind: JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null } value => value.GetRawText(),
            _ => null,
        };
        try
        {
            return text is not null && Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, Matching.Timeout);
        }
        catch (RegexMatchTimeoutException)
        {
            timedOut = true;
            return false;
        }
    }

    private static bool GoldenHolds(string output, JsonDocument? document, string? goldenText)
    {
        if (goldenText is null)
        {
            return false;
        }

        using var golden = Parse(goldenText);
        return document is not null && golden is not null ? JsonEquality.Equal(document.RootElement, golden.RootElement) : string.Equals(output, goldenText, StringComparison.Ordinal);
    }

    private static JsonDocument? Parse(string text)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class Tally
    {
        private readonly List<ApiFailure> _failures = [];
        private int _count;
        private int? _first;

        internal void Add(bool holds, ApiFailure? failure)
        {
            _count++;
            if (!holds && failure is not null)
            {
                _failures.Add(failure);
                _first ??= _count;
            }
        }

        internal CheckedExpectation Result() => new(_count, _failures, _first);
    }
}
