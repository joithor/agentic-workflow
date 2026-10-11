using System.Globalization;
using System.Text.Json;
using Aw.Kernel;

namespace Aw.Evidence.Core.Scripts;

/// <summary>Reads a UI script document and checks its rules.</summary>
public static class ScriptReader
{
    /// <summary>Parses and validates a script.</summary>
    /// <param name="utf8Json">The script file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The script, or AW-EVIDENCE-002 naming every problem.</returns>
    public static Result<UiScript> Read(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) =>
        JsonInput.Read<UiScript>(utf8Json, options, EvidenceErrors.ScriptInvalid).Bind(Validate);

    /// <summary>Checks a script's rules: at least one step and viewport, known enum values, well-formed expectations, non-negative usage.</summary>
    /// <param name="script">The script.</param>
    /// <returns>The script, or AW-EVIDENCE-002 naming every problem.</returns>
    public static Result<UiScript> Validate(UiScript script)
    {
        var problems = new Problems();
        problems.AddIf(!Enum.IsDefined(script.Role), "role is not a known role");
        problems.AddIf(script.Steps.Count == 0, "steps needs at least one step");
        problems.AddIf(script.Viewports.Count == 0, "viewports needs at least one viewport");
        problems.AddIf(script.Viewports.Any(static v => !Enum.IsDefined(v)), "viewports holds an unknown viewport");
        for (var i = 0; i < script.Steps.Count; i++)
        {
            CheckStep(script.Steps[i], i + 1, problems);
        }

        if (script.Planning is { } planning)
        {
            problems.AddIf(planning.ElapsedMs < 0 || planning.InputTokens < 0 || planning.OutputTokens < 0, "planning numbers must not be negative");
        }

        return problems.Into(script, EvidenceErrors.ScriptInvalid);
    }

    private static void CheckStep(ScriptStep? step, int number, Problems problems)
    {
        var at = string.Create(CultureInfo.InvariantCulture, $"step {number}");
        if (step is null)
        {
            problems.Add($"{at} is null");
            return;
        }

        problems.AddIf(!Enum.IsDefined(step.Action), $"{at}: action is not a known action");
        if (step.ExpectedState.Problem() is { } problem)
        {
            problems.Add($"{at}: {problem}");
        }
    }
}
