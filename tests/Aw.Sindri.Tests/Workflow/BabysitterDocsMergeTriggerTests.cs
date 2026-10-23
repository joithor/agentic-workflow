using System.Text.RegularExpressions;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The babysitter's two calls of 'effect.pr.merge-docs' say what triggered them (PB spec §4.7): the click that merged the PR, or the owner's desk. The recipe language has no reference to a caller, so no actor.</summary>
public sealed partial class BabysitterDocsMergeTriggerTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_docs_merge_after_the_click_and_the_one_from_the_owners_desk_name_their_trigger()
    {
        var calls = MergeDocsCalls.Matches(ShippedRecipes.Read("sindri/workflow.pr-babysitter.yaml")).Select(static m => m.Value).ToList();

        Assert.Equal(2, calls.Count);
        Assert.Contains(calls, static c => c.Contains("trigger: command.merge", StringComparison.Ordinal));
        Assert.Contains(calls, static c => c.Contains("trigger: ownerDesk", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"use: effect\.pr\.merge-docs, with: \{[^}]*\{[^}]*\}[^}]*\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MergeDocsCalls { get; }
}
