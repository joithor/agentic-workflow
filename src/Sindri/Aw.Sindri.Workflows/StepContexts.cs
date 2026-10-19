using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Builds the <see cref="StepContext"/> of an in-process Step run inside its own activity (plan "How the text reaches the Step"; W6):
/// the injection sections and the typed <see cref="StepContext.InputResults"/> come from the in-memory texts the 'FillInput'
/// activity kept. On a miss (a restart, an eviction) the input runs again and its digests must equal the recorded ones: a Step never
/// runs on data its plan does not describe (SND-WF-018).
/// </summary>
/// <param name="texts">The in-memory texts.</param>
/// <param name="runner">Runs an input again.</param>
/// <param name="repos">The ledger's repo of an item, for an instance with no fact:item of its own (a child).</param>
internal sealed class StepContexts(IInjectionTexts texts, IStepInputRunner runner, IItemRepos? repos = null)
{
    /// <summary>
    /// The instance's repo, three ways: a repo, none (the ledger knows none), or one that cannot be read, which fails the Step. A top-level instance's comes from its fact:item;
    /// a child has no fact:item, so its address's item is looked up in the ledger, as the agent runtime does (AgentTargetResolver).
    /// </summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="named">The repo its fact:item names, or null.</param>
    /// <param name="unreadable">Whether its fact:item could not be read.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <param name="lookup">False when the caller needs no repo that the instance does not name itself: the ledger is not asked, so a transient ledger error cannot fail a Step that never reads a repo.</param>
    /// <returns>The repo or none; SND-BLOCK-003 when it cannot be read or is no repo name.</returns>
    public async ValueTask<Result<RepoName?>> RepoOf(string address, string? named, bool unreadable, CancellationToken cancellationToken, bool lookup = true)
    {
        ArgumentNullException.ThrowIfNull(address);
        var refused = WorkflowErrors.BlockRepoUnreadable.With("repo-unreadable");
        if (unreadable)
        {
            return refused;
        }

        if (named is not null)
        {
            return RepoName.TryFrom(named, out var name) ? (RepoName?)name : refused;
        }

        if (!lookup || repos is null || !ItemId.TryFrom(address.Split('/')[0], out var item))
        {
            return (RepoName?)null;
        }

        var found = await repos.RepoOf(item, cancellationToken).ConfigureAwait(false);
        return found.IsError ? refused : found.Value;
    }

    /// <summary>The input's context with the instance's repo: the one it carries, else the three-way lookup (a child's item is in the address).</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="input">The input's context.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The context; SND-BLOCK-003 when the repo cannot be read.</returns>
    public async ValueTask<Result<StepInputContext>> WithRepo(string address, StepInputContext input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Repo is not null)
        {
            return input;
        }

        var repo = await RepoOf(address, named: null, unreadable: false, cancellationToken).ConfigureAwait(false);
        return repo.IsError ? repo.Error : input with { Repo = repo.Value };
    }

    /// <summary>The context <paramref name="run"/> starts with.</summary>
    /// <param name="run">The run.</param>
    /// <param name="seed">The context without the inputs' contribution.</param>
    /// <param name="inputs">The Step's inputs in declaration order.</param>
    /// <param name="cancellationToken">Cancels the refill.</param>
    /// <returns>The context; SND-WF-018 when a refilled input differs from what was recorded.</returns>
    public async ValueTask<Result<StepContext>> For(StepRun run, StepContext seed, IReadOnlyList<InjectionInputSource> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(inputs);
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        var results = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var input in inputs.OrderBy(static i => i.Recorded.Index))
        {
            var scoped = await WithRepo(run.Address, input.Context, cancellationToken).ConfigureAwait(false);
            if (scoped.IsError)
            {
                return scoped.Error;
            }

            var kept = await InputRefills.Texts(texts, runner, new InputRefill(run.Address, run.State, run.Token, input with { Context = scoped.Value }, WorkflowErrors.InputChanged), cancellationToken).ConfigureAwait(false);
            if (kept.IsError)
            {
                return kept.Error;
            }

            foreach (var (name, text) in kept.Value.Sections)
            {
                sections[name] = text;
            }

            if (kept.Value.Result is { } result)
            {
                results[ResultKey(input.Recorded, inputs)] = result;
            }
        }

        return seed with { Injection = new InjectionSections(sections), InputResults = results };
    }

    /// <summary>The key of an entry's typed result: the input id, or 'id#n' (n = the entry's place) when the Step lists that input more than once, so the second use never overwrites the first.</summary>
    private static string ResultKey(FilledInput entry, IReadOnlyList<InjectionInputSource> all) =>
        all.Where(i => i.Recorded.Input == entry.Input).Skip(1).Any() ? $"{entry.Input.Value}#{entry.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : entry.Input.Value;
}
