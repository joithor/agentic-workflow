using System.Text.RegularExpressions;

namespace Aw.Evidence.Core.Design;

/// <summary>The rules a design manifest's parts must meet beyond its JSON shape.</summary>
internal static partial class ManifestRules
{
    internal static void CheckManifest(DesignManifest m, Problems problems)
    {
        problems.AddIf(!m.Route.StartsWith('/'), "route must start with '/'");
        problems.AddIf(!m.Ready.IsWellFormed, "ready needs exactly one of testId and text");
        problems.AddIf(m.Viewport.W <= 0 || m.Viewport.H <= 0, "viewport needs a positive width and height");
        problems.AddIf(m.TimeoutMs <= 0, "timeoutMs must be positive");
        problems.AddIf(m.SettleMs < 0, "settleMs must not be negative");
        problems.AddIf(m.Threshold is < 0 or > 1 || double.IsNaN(m.Threshold), "threshold must be between 0 and 1");
        problems.AddIf(m.Frames.Count == 0, "frames needs at least one frame");
        if (m.Login is { } login)
        {
            CheckLogin(login, problems);
        }

        var duplicate = m.Frames.GroupBy(static f => f.Name, StringComparer.Ordinal).FirstOrDefault(static g => g.Skip(1).Any());
        problems.AddIf(duplicate is not null, $"duplicate frame name \"{duplicate?.Key}\"");
    }

    internal static void CheckFrame(DesignFrame f, Problems problems)
    {
        var at = $"frame \"{f.Name}\"";
        problems.AddIf(!IsSafeName(f.Name), $"{at}: name must be letters, digits, '_' or '-', starting with a letter or digit");
        problems.AddIf(f.DesignPng.Length == 0, $"{at}: designPng is empty");
        var r = f.DesignRegion;
        problems.AddIf(r.X < 0 || r.Y < 0 || r.W <= 0 || r.H <= 0, $"{at}: designRegion needs x,y >= 0 and w,h > 0");
        problems.AddIf(!f.Anchor.IsWellFormed, $"{at}: anchor needs exactly one of testId and text");
        problems.AddIf(f.WaitFor is { IsWellFormed: false }, $"{at}: waitFor needs exactly one of testId and text");
        problems.AddIf(f.FormValues.Any(static v => v is null || !v.IsWellFormed), $"{at}: each formValue needs exactly one of testId and css");
        problems.AddIf(f.KnownDifferences.Any(string.IsNullOrEmpty), $"{at}: knownDifferences holds an empty entry");
        foreach (var e in f.ExpectBoxes)
        {
            CheckExpectation(e, at, problems);
        }
    }

    private static void CheckExpectation(BoxExpectation? e, string at, Problems problems)
    {
        if (e is null)
        {
            problems.Add($"{at}: expectBoxes holds a null entry");
            return;
        }

        problems.AddIf(!e.Target.IsWellFormed, $"{at}: an expectBoxes target needs exactly one of testId and text");
        problems.AddIf(e.RelativeTo is { IsWellFormed: false }, $"{at}: an expectBoxes relativeTo needs exactly one of testId and text");
        problems.AddIf(e.Expected.IsEmpty, $"{at}: expected needs at least one of w, h, x and y");
        problems.AddIf(e.Tolerance < 0, $"{at}: tolerance must not be negative");
    }

    private static void CheckLogin(LoginSpec login, Problems problems)
    {
        problems.AddIf(!IsEnvName(login.EmailEnv) || !IsEnvName(login.PasswordEnv), "login emailEnv and passwordEnv must be UPPER_SNAKE environment variable names");
        problems.AddIf(
            login.EmailSelector.Length == 0 || login.PasswordSelector.Length == 0 || login.SubmitSelector.Length == 0,
            "login selectors must not be empty");
    }

    private static bool IsSafeName(string name) => SafeName.IsMatch(name);

    private static bool IsEnvName(string name) => EnvName.IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex SafeName { get; }

    [GeneratedRegex("^[A-Z][A-Z0-9_]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex EnvName { get; }
}
