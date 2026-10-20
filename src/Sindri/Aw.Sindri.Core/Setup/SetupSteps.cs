using System.Globalization;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The pure decision of each setup step (GET /sindri/setup): one function per step from the fact its source gave, and <see cref="Unreadable"/> for a source that couldn't be read.
/// Titles, reasons and details are plain words; an error code appears only in <see cref="SetupError.Code"/>.
/// </summary>
public static class SetupSteps
{
    private const string RepoTitle = "Add your repo";
    private const string RepoWhy = "Sindri works on the code you point it at, so it needs at least one repository.";
    private const string ProfileTitle = "Approve your profile";
    private const string ProfileWhy = "Your profile says what Sindri may do on this machine, and nothing runs until you approve it.";
    private const string RecipesTitle = "Approve the workflows";
    private const string RecipesWhy = "Workflows are the step-by-step recipes Sindri follows, and each one needs your approval before it can run.";
    private const string McpTitle = "Connect your coding agent";
    private const string McpWhy = "Registering Sindri with your agent, for each repo only, lets the agent ask Sindri for context and report back.";
    private const string MapTitle = "Map your code";
    private const string MapWhy = "The map is how Sindri knows how your code, tickets and decisions connect, so what it tells an agent is relevant.";
    private const string OnyxTitle = "Set up Onyx";
    private const string OnyxWhy = "Onyx is the search Sindri uses to find your business context, such as decisions and tickets.";

    /// <summary>The repo step: done once the profile names a repo.</summary>
    /// <param name="fact">The repos.</param>
    /// <returns>The step.</returns>
    public static SetupStep Repo(RepoFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Count > 0
            ? new SetupStep(SetupStepId.Repo, RepoTitle, SetupState.Done, RepoWhy, Counted(fact.Count, "repository", "repositories") + " added.", Error: null, Action: null, Progress: null)
            : new SetupStep(SetupStepId.Repo, RepoTitle, SetupState.Todo, RepoWhy, "No repository is added yet.", Error: null, SetupActions.AddRepo, Progress: null);
    }

    /// <summary>The profile step: done when approved, the human's when it is waiting or has drifted.</summary>
    /// <param name="fact">Where the profile stands.</param>
    /// <returns>The step.</returns>
    public static SetupStep Profile(ProfileFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Presence switch
        {
            ProfilePresence.Approved => new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.Done, ProfileWhy, "Your profile is approved.", Error: null, Action: null, Progress: null),
            ProfilePresence.Unapproved => new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.NeedsYou, ProfileWhy, "Your profile is ready for you to read and approve.", Error: null, SetupActions.ApproveProfile, Progress: null),
            ProfilePresence.Drifted => new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.NeedsYou, ProfileWhy, "Your profile changed since you approved it. Read what changed and approve it again.", Error: null, SetupActions.ApproveProfile, Progress: null),
            ProfilePresence.Missing => new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.Todo, ProfileWhy, "There is no profile yet.", Error: null, Action: null, Progress: null),
            _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.Presence, "Not a profile presence"),
        };
    }

    /// <summary>The recipes step: done when every recipe is approved, the human's while some wait.</summary>
    /// <param name="fact">Where the recipes stand.</param>
    /// <returns>The step.</returns>
    public static SetupStep Recipes(RecipesFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var progress = new SetupProgress(fact.Approved, fact.Total);
        if (fact.Total == 0)
        {
            return new SetupStep(SetupStepId.Recipes, RecipesTitle, SetupState.Todo, RecipesWhy, "No workflows are registered yet. Sindri registers them when it starts.", Error: null, Action: null, Progress: null);
        }

        var waiting = fact.Total - fact.Approved;
        return waiting <= 0
            ? new SetupStep(SetupStepId.Recipes, RecipesTitle, SetupState.Done, RecipesWhy, "All " + Counted(fact.Total, "workflow", "workflows") + " approved.", Error: null, Action: null, progress)
            : new SetupStep(SetupStepId.Recipes, RecipesTitle, SetupState.NeedsYou, RecipesWhy, Counted(waiting, "workflow", "workflows") + " waiting for approval.", Error: null, SetupActions.ApproveAllRecipes, progress);
    }

    /// <summary>The MCP step: done when every repo has its local registration.</summary>
    /// <param name="fact">Where the registration stands.</param>
    /// <returns>The step.</returns>
    public static SetupStep Mcp(McpFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact.Repos == 0)
        {
            return new SetupStep(SetupStepId.Mcp, McpTitle, SetupState.Todo, McpWhy, "Add your repo and approve your profile first.", Error: null, Action: null, Progress: null);
        }

        if (fact.Registered >= fact.Repos)
        {
            return new SetupStep(SetupStepId.Mcp, McpTitle, SetupState.Done, McpWhy, "Your agent knows where Sindri is in " + Counted(fact.Repos, "repository", "repositories") + ".", Error: null, Action: null, Progress: null);
        }

        var detail = fact.Repos == 1
            ? "Your agent doesn't know where Sindri is yet."
            : string.Create(CultureInfo.InvariantCulture, $"Your agent knows where Sindri is in {fact.Registered} of {fact.Repos} repositories.");
        return new SetupStep(SetupStepId.Mcp, McpTitle, SetupState.Todo, McpWhy, detail, Error: null, SetupActions.RegisterMcp, fact.Repos == 1 ? null : new SetupProgress(fact.Registered, fact.Repos));
    }

    /// <summary>The map step: it builds on its own, so it has no action.</summary>
    /// <param name="fact">Where the map stands.</param>
    /// <returns>The step.</returns>
    public static SetupStep Map(MapFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Phase switch
        {
            MapPhase.Built => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Done, MapWhy, "The map is built.", Error: null, Action: null, Progress: null),
            MapPhase.Building => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Running, MapWhy,
                fact.Progress is { } p ? string.Create(CultureInfo.InvariantCulture, $"Sindri is building the map: {p.Done} of {p.Total} done.") : "Sindri is building the map.", Error: null, Action: null, fact.Progress),
            MapPhase.Failed => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Failed, MapWhy, "The last try at building the map didn't finish.",
                new SetupError(fact.FailureCode ?? "SND-CONTEXT-042", "The last try at building the map didn't finish."), Action: null, Progress: null),
            MapPhase.NotBuilt => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Todo, MapWhy, "The map isn't built yet. Sindri builds it on its own.", Error: null, Action: null, Progress: null),
            MapPhase.Waiting => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Todo, MapWhy, "Sindri builds the map once your repo is added and your profile is approved.", Error: null, Action: null, Progress: null),
            _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.Phase, "Not a map phase"),
        };
    }

    /// <summary>The Onyx step: done when the pinned Onyx answers; failed when this machine can't run it or it doesn't answer.</summary>
    /// <param name="fact">Where Onyx stands.</param>
    /// <returns>The step.</returns>
    public static SetupStep Onyx(OnyxFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact.State == OnyxState.Running)
        {
            return fact.Unreachable
                ? Failed(SetupStepId.Onyx, OnyxTitle, OnyxWhy, new SetupError("SND-ONYX-005", OnyxSentence("SND-ONYX-005")))
                : new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Done, OnyxWhy, "Onyx is up and answering.", Error: null, Action: null, Progress: null);
        }

        if (fact.State == OnyxState.Unknown || fact.BlockedByCode is not null)
        {
            var code = fact.BlockedByCode ?? "SND-ONYX-001";
            return Failed(SetupStepId.Onyx, OnyxTitle, OnyxWhy, new SetupError(code, OnyxSentence(code)));
        }

        return new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Todo, OnyxWhy,
            fact.State == OnyxState.Stopped ? "Onyx is set up but stopped." : "Onyx isn't set up yet.", Error: null, SetupActions.SetupOnyx, Progress: null);
    }

    /// <summary>The step for a source that couldn't be read: failed, with one plain sentence and the error's code.</summary>
    /// <param name="id">Which step.</param>
    /// <param name="code">The code of the error the read returned.</param>
    /// <returns>The step.</returns>
    public static SetupStep Unreadable(SetupStepId id, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var (title, why, sentence) = id switch
        {
            SetupStepId.Repo => (RepoTitle, RepoWhy, "Sindri couldn't read your profile, so it can't tell which repositories are added."),
            SetupStepId.Profile => (ProfileTitle, ProfileWhy, "Your profile can't be read. It has a mistake, or the file is damaged."),
            SetupStepId.Recipes => (RecipesTitle, RecipesWhy, "Sindri couldn't read the list of workflows."),
            SetupStepId.Mcp => (McpTitle, McpWhy, "Sindri couldn't check whether your agent knows where it is."),
            SetupStepId.Map => (MapTitle, MapWhy, "Sindri couldn't read the state of the map."),
            SetupStepId.Onyx => (OnyxTitle, OnyxWhy, "Sindri couldn't check on Onyx."),
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Not a setup step"),
        };
        return Failed(id, title, why, new SetupError(code, sentence));
    }

    private static SetupStep Failed(SetupStepId id, string title, string why, SetupError error) =>
        new(id, title, SetupState.Failed, why, error.Sentence, error, Action: null, Progress: null);

    private static string OnyxSentence(string code) => code switch
    {
        "SND-ONYX-001" => "Docker isn't installed or isn't running, and Onyx runs in Docker.",
        "SND-ONYX-002" => "Docker Compose isn't available, and Onyx needs it.",
        "SND-ONYX-003" => "There isn't enough free memory or disk space to run Onyx.",
        "SND-ONYX-005" => "Onyx is running but isn't answering.",
        "SND-ONYX-006" => "The port Onyx needs is taken by something else.",
        "SND-ONYX-010" => "Docker points at another machine, and Sindri only runs Onyx on this one.",
        _ => "Onyx can't be set up on this machine right now.",
    };

    private static string Counted(int count, string one, string many) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? one : many)}");
}
