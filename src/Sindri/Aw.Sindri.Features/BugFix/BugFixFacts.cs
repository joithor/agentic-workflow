using System.Text.Json.Nodes;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.BugFix;

/// <summary>What the four bugfix Steps share: bugfix-state's directory in the instance's run area, the fact readers and the failure for an answer that is not a value.</summary>
internal static class BugFixFacts
{
    /// <summary>The name of bugfix-state's directory in the run area.</summary>
    internal const string StateName = "bugfix";

    /// <summary>The bug fix's state directory for the instance; a failure when the context has no address or the run area cannot place it.</summary>
    /// <param name="step">The Step's id, for the failure.</param>
    /// <param name="context">The Step's context.</param>
    /// <param name="area">The run area.</param>
    /// <returns>The directory (empty when it failed), or the failed result.</returns>
    internal static (string Directory, BlockStepResult? Failure) StateDirectory(string step, StepContext context, IRunArea area)
    {
        if (string.IsNullOrEmpty(context.Address))
        {
            return (string.Empty, BlockStepResult.Failed($"{step} has no instance address to place the bug fix state under"));
        }

        var placed = area.PathFor(context.Address, StateName);
        return placed.IsError
            ? (string.Empty, BlockStepResult.Failed($"{step} can't place the bug fix state: {placed.Error.Message}"))
            : (placed.Value, null);
    }

    /// <summary>The text of a member of an object fact.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="name">The member.</param>
    /// <returns>The text, or null.</returns>
    internal static string? Text(JsonNode? fact, string name) => fact is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    /// <summary>The whole number of a member of an object fact.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="name">The member.</param>
    /// <returns>The number, or null.</returns>
    internal static int? Int(JsonNode? fact, string name) => fact is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    /// <summary>The flag of a member of an object fact; false when absent.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="name">The member.</param>
    /// <returns>The flag.</returns>
    internal static bool Flag(JsonNode? fact, string name) => fact is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    /// <summary>The candidate's id and worktree from 'fact:candidate'.</summary>
    /// <param name="candidate">The fact.</param>
    /// <returns>The pair, or null when the fact lacks either.</returns>
    internal static (string Id, string? Worktree)? Candidate(JsonNode? candidate) => Text(candidate, "id") is { } id ? (id, Text(candidate, "worktree")) : null;

    /// <summary>The failure for an answer that is not a value: a refusal names bugfix-state's code and reason, an unavailable tool its reason.</summary>
    /// <typeparam name="T">The answer's value.</typeparam>
    /// <param name="step">The Step's id.</param>
    /// <param name="answer">The answer.</param>
    /// <returns>The failed result.</returns>
    internal static BlockStepResult Failure<T>(string step, BugFixAnswer<T> answer) => BlockStepResult.Failed(answer.Refusal is { } refusal
        ? $"{step}: bugfix-state refused: {refusal.Code} {refusal.Message}"
        : $"{step}: {answer.Unavailable}");
}
