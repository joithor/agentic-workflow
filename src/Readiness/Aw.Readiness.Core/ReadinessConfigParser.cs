using System.Globalization;
using Aw.Kernel;

namespace Aw.Readiness.Core;

/// <summary>Reads readiness.yaml's flat subset line by line. Every refusal names the line, the key and the fix.</summary>
internal static class ReadinessConfigParser
{
    private static readonly string[] _requiredKeys = ["version", "repo", "ring", "scope", "targets"];

    private const string Fix = "fix: see docs/readiness/README.md for the readiness.yaml format";

    internal static Result<ReadinessConfig> Parse(string text)
    {
        var top = new Dictionary<string, (string Value, int Line)>(StringComparer.Ordinal);
        var targets = new List<(int? Ring, int? Level, int Line)>();
        var inTargets = false;
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripComment(lines[i]);
            if (line.Trim().Length == 0)
            {
                continue;
            }

            var error = line[0] == ' '
                ? TargetLine(line, i + 1, inTargets, targets)
                : TopLine(line, i + 1, top, ref inTargets);
            if (error is not null)
            {
                return error;
            }
        }

        return Build(top, targets);
    }

    private static Error? TopLine(string line, int number, Dictionary<string, (string, int)> top, ref bool inTargets)
    {
        if (inTargets && line[0] is '-' or '\t')
        {
            return Refuse(number, "targets", "indent target items with spaces under 'targets:' (two spaces, then '- ring: N')");
        }

        var (key, value) = Split(line);
        if (key is not ("version" or "repo" or "ring" or "scope" or "targets"))
        {
            return Refuse(number, key, $"unknown key '{key}'");
        }

        if (top.ContainsKey(key))
        {
            return Refuse(number, key, $"'{key}' is set twice");
        }

        top[key] = (value, number);
        inTargets = key == "targets";
        return inTargets && value.Length != 0 ? Refuse(number, key, "'targets' takes a list on the following lines") : null;
    }

    private static Error? TargetLine(string line, int number, bool inTargets, List<(int? Ring, int? Level, int Line)> targets)
    {
        var trimmed = line.TrimStart();
        var startsItem = trimmed.StartsWith("- ", StringComparison.Ordinal);
        var (key, value) = Split(startsItem ? trimmed[2..] : trimmed);
        if (!inTargets || (!startsItem && targets.Count == 0))
        {
            return Refuse(number, key, "an indented line belongs under 'targets:'");
        }

        var (n, refused) = TargetValue(number, key, value, trimmed);
        if (refused is not null)
        {
            return refused;
        }

        if (startsItem)
        {
            targets.Add((null, null, number));
        }

        return Assign(targets, number, key, n);
    }

    private static Error? Assign(List<(int? Ring, int? Level, int Line)> targets, int number, string key, int n)
    {
        var item = targets[^1];
        if ((key == "ring" && item.Ring is not null) || (key == "level" && item.Level is not null))
        {
            return Refuse(number, key, $"'{key}' is set twice in this target");
        }

        targets[^1] = key == "ring" ? item with { Ring = n } : item with { Level = n };
        return null;
    }

    private static (int Value, Error? Refused) TargetValue(int number, string key, string value, string trimmed) =>
        key is not ("ring" or "level") || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? (0, Refuse(number, key, $"a target takes 'ring: N' and 'level: N', not '{trimmed}'"))
            : key == "level" && n is < 1 or > 5 ? (0, Refuse(number, key, $"level {N(n)} is outside 1-5"))
            : (n, null);

    private static Result<ReadinessConfig> Build(
        Dictionary<string, (string Value, int Line)> top, List<(int? Ring, int? Level, int Line)> targets)
    {
        foreach (var key in _requiredKeys.Where(k => !top.ContainsKey(k)))
        {
            return Refuse(0, key, $"'{key}' is missing");
        }

        var refused = Check(top, targets);
        if (refused is not null)
        {
            return refused;
        }

        var ring = int.Parse(top["ring"].Value, CultureInfo.InvariantCulture);
        var config = new ReadinessConfig(
            RepoName.From(top["repo"].Value), ring, top["scope"].Value,
            [.. targets.Select(t => new ReadinessTarget(t.Ring!.Value, t.Level!.Value))]);   // why: Check refused any item without both
        return config.Targets.Any(t => t.Ring == ring)
            ? config
            : Refuse(top["ring"].Line, "ring", $"ring {N(ring)} has no entry under 'targets'");
    }

    private static Error? Check(Dictionary<string, (string Value, int Line)> top, List<(int? Ring, int? Level, int Line)> targets)
    {
        var (version, versionLine) = top["version"];
        var (repo, repoLine) = top["repo"];
        var (ring, ringLine) = top["ring"];
        var (scope, scopeLine) = top["scope"];
        var duplicate = targets.GroupBy(t => t.Ring).FirstOrDefault(g => g.Skip(1).Any());
        var incompleteLine = targets.Where(t => t.Ring is null || t.Level is null).Select(t => t.Line).FirstOrDefault();
        return version != "1" ? Refuse(versionLine, "version", $"version '{version}' is not supported (only 1)")
            : !RepoName.TryFrom(repo, out _) ? Refuse(repoLine, "repo", $"repo '{repo}' is not a valid name (a-z, 0-9, '.', '_', '-')")
            : !int.TryParse(ring, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? Refuse(ringLine, "ring", $"ring '{ring}' is not a number")
            : scope != ReadinessConfig.CSharpTemplateScope ? Refuse(scopeLine, "scope", $"scope '{scope}' is not supported (only {ReadinessConfig.CSharpTemplateScope})")
            : targets.Count == 0 ? Refuse(top["targets"].Line, "targets", "'targets' lists no ring")
            : incompleteLine != 0 ? Refuse(incompleteLine, "targets", "a target needs both 'ring' and 'level'")
            : duplicate is not null ? Refuse(duplicate.Last().Line, "ring", $"ring {N(duplicate.Key ?? 0)} is listed twice under 'targets'")
            : null;
    }

    private static (string Key, string Value) Split(string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (line.Trim(), "") : (line[..colon].Trim(), line[(colon + 1)..].Trim());
    }

    private static string StripComment(string line)
    {
        var hash = line.StartsWith('#') ? 0 : line.IndexOf(" #", StringComparison.Ordinal);
        return hash < 0 ? line : line[..hash];
    }

    private static Error Refuse(int line, string key, string problem) => ReadinessErrors.ConfigInvalid.With(
        line == 0 ? $"{ReadinessConfig.FileName}: {problem}; {Fix}" : $"{ReadinessConfig.FileName} line {N(line)} ({key}): {problem}; {Fix}");

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
