using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>The babysitter's figures as groups of lines in plain words (api #778), for the recipe sheet's stats below the run figures. A group with nothing to say is left out.</summary>
internal static class RecipeBabysitterGroups
{
    /// <summary>The groups for an answer.</summary>
    /// <param name="b">The babysitter's figures.</param>
    /// <returns>The groups, in order.</returns>
    internal static IReadOnlyList<RecipeStatGroup> Of(BabysitterStatsView b)
    {
        ArgumentNullException.ThrowIfNull(b);
        var groups = new List<RecipeStatGroup>();
        Add(groups, Labels.BabysitterPullsHeading, Pulls(b));
        Add(groups, Labels.BabysitterCommentsHeading, Comments(b.Comments));
        Add(groups, Labels.BabysitterCiHeading, CiFix(b.CiFix));
        Add(groups, Labels.BabysitterRechecksHeading, Rechecks(b.Rechecks));
        Add(groups, Labels.BabysitterOwnerHeading, Owner(b.OwnerActionsAtReady));
        Add(groups, Labels.BabysitterDocsHeading, Docs(b.Docs));
        return groups;
    }

    private static void Add(List<RecipeStatGroup> groups, string heading, List<RecipeStatLine> lines)
    {
        if (lines.Count > 0)
        {
            groups.Add(new RecipeStatGroup(heading, lines));
        }
    }

    private static List<RecipeStatLine> Pulls(BabysitterStatsView b)
    {
        List<RecipeStatLine> lines =
        [
            new(Labels.BabysitterPulls, Labels.StatCount(b.Pulls)),
            new(Labels.BabysitterToReady, Labels.BabysitterSpread(b.SinceStartToReadySeconds.Median, b.SinceStartToReadySeconds.P90, Labels.Duration)),
            new(Labels.BabysitterToMerge, Labels.BabysitterSpread(b.SinceStartToMergeSeconds.Median, b.SinceStartToMergeSeconds.P90, Labels.Duration)),
            new(Labels.BabysitterTokens, Labels.BabysitterSpread(b.TokensPerPull.Median, b.TokensPerPull.P90, Labels.StatCount)),
        ];
        if (b.RecordErrors > 0)
        {
            lines.Add(new RecipeStatLine(string.Empty, Labels.BabysitterRecordErrors(b.RecordErrors)));
        }

        return lines;
    }

    private static List<RecipeStatLine> Comments(BabysitterCommentsView c)
    {
        var lines = new List<RecipeStatLine>();
        if (c.Total == 0)
        {
            return lines;
        }

        lines.Add(new RecipeStatLine(Labels.BabysitterCommentsTotal, Labels.StatCount(c.Total)));
        lines.Add(new RecipeStatLine(Labels.BabysitterCommentsHeld, Labels.StatCount(c.Held)));
        lines.Add(new RecipeStatLine(Labels.BabysitterCommentsWithout, Labels.StatCount(c.WithoutOwner)));
        lines.AddRange(c.ByType.Select(t => new RecipeStatLine(t.Type, Labels.BabysitterCommentRow(CommentParts(t)))));   // a type this build doesn't know shows as served
        return lines;
    }

    private static List<string> CommentParts(BabysitterCommentTypeView t)
    {
        var parts = new List<string>();
        AddPart(parts, "fixed", t.Fixed);
        AddPart(parts, "replied", t.Replied);
        AddPart(parts, "reacted", t.Reacted);
        AddPart(parts, "held", t.Held);
        AddPart(parts, "skipped", t.Skipped);
        return parts;
    }

    private static void AddPart(List<string> parts, string word, int n)
    {
        if (n > 0)
        {
            parts.Add(word + " " + Labels.StatCount(n));
        }
    }

    private static List<RecipeStatLine> CiFix(BabysitterCiFixView c) =>
        c.Attempts == 0
            ? []
            :
            [
                new(Labels.BabysitterCiAttempts, Labels.StatCount(c.Attempts)),
                new(Labels.BabysitterCiSucceeded, Labels.StatCount(c.Succeeded)),
                new(Labels.BabysitterCiStillRed, Labels.StatCount(c.StillRed)),
                new(Labels.BabysitterCiRate, c.Rate is { } rate ? Labels.Percent(rate) : Labels.BabysitterNotEnough),
            ];

    private static List<RecipeStatLine> Rechecks(BabysitterRechecksView r) =>
        r.Runs == 0
            ? []
            :
            [
                new(Labels.BabysitterRechecksRuns, Labels.StatCount(r.Runs)),
                new(Labels.BabysitterRechecksResolved, Labels.StatCount(r.Resolved)),
                new(Labels.BabysitterRechecksUnresolved, Labels.StatCount(r.Unresolved)),
            ];

    private static List<RecipeStatLine> Owner(BabysitterOwnerActionsView o)
    {
        var lines = new List<RecipeStatLine>();
        Add(lines, Labels.BabysitterOwnerMerge, o.Merge);
        Add(lines, Labels.BabysitterOwnerRework, o.Rework);
        Add(lines, Labels.BabysitterOwnerComments, o.Comments);
        Add(lines, Labels.BabysitterOwnerResync, o.ResyncDocs);
        Add(lines, Labels.BabysitterOwnerCheck, o.Check);
        return lines;
    }

    private static List<RecipeStatLine> Docs(BabysitterDocsView d)
    {
        var lines = new List<RecipeStatLine>();
        Add(lines, Labels.BabysitterDocsWithMerge, d.WithMerge);
        Add(lines, Labels.BabysitterDocsOwnerDesk, d.OwnerDesk);
        lines.AddRange(d.ByOutcome.Select(o => new RecipeStatLine(Labels.BabysitterDocsOutcome(o.Reason), Labels.StatCount(o.N))));
        return lines;
    }

    private static void Add(List<RecipeStatLine> lines, string label, int n)
    {
        if (n > 0)
        {
            lines.Add(new RecipeStatLine(label, Labels.StatCount(n)));
        }
    }
}
