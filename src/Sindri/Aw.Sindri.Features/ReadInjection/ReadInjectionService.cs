using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.ReadInjection;

/// <summary>
/// The text behind the open run's plan, pulled on demand (plan "How the text reaches the Step"): the plan names the sections, the
/// in-memory texts hold them, and a miss (a restart, an eviction) runs the inputs again outside the workflow. A refill is served only
/// when it equals what the run recorded; otherwise nothing is served and the answer is SND-WF-017. Nothing is recorded. A run that is not
/// open is SND-WF-005; an unknown instance, or another repo's for a session caller, is the fact route's one not-found answer.
/// </summary>
/// <param name="directory">Resolves the instance id to its address.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="sources">The open run's inputs.</param>
/// <param name="texts">The in-memory texts.</param>
/// <param name="runner">Runs an input again.</param>
/// <param name="repos">Where each item belongs, from the ledger.</param>
/// <param name="callers">The caller, whose repo scope decides what it may read and rides on the refill.</param>
internal sealed class ReadInjectionService(IInstanceDirectory directory, IInstanceReader runtime, IInjectionSources sources, IInjectionTexts texts, IStepInputRunner runner, IItemRepos repos, ICallerContextAccessor callers)
    : IService<ReadInjectionRequest, InjectionView>
{
    public async ValueTask<Result<InjectionView>> Handle(ReadInjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("an injection read needs a caller");
        }

        var address = await directory.Resolve(request.Instance.Value, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return InstanceScope.Unknown(address.Error, request.Instance.Value);
        }

        var view = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return InstanceScope.Unknown(view.Error, request.Instance.Value);
        }

        if (await InstanceScope.Refusal(caller, view.Value.ItemId, request.Instance.Value, repos, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (view.Value.Run is null || view.Value.Task is not { } task)
        {
            return WorkflowErrors.StaleRunToken.With($"{address.Value.Value} has no open run, so there is no injection text (fix: sindri workflow status)");
        }

        var wanted = task.Injection.Where(e => request.Section is null || string.Equals(e.Section, request.Section, StringComparison.Ordinal)).ToList();
        if (request.Section is not null && wanted.Count == 0)
        {
            return SindriErrors.ItemNotFound.With($"the open run's plan has no section '{request.Section}'");
        }

        var source = await sources.Read(address.Value, cancellationToken).ConfigureAwait(false);
        return source.IsError ? source.Error : await Sections(address.Value.Value, source.Value, wanted, caller, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<InjectionView>> Sections(string address, InjectionSource source, List<InjectionEntry> wanted, CallerContext caller, CancellationToken cancellationToken)
    {
        var scope = caller.Class == CallerClass.Session ? caller.Repo : null;
        var filled = new Dictionary<string, FilledTexts>(StringComparer.Ordinal);
        var sections = new List<InjectionSectionView>();
        foreach (var entry in wanted)
        {
            if (!filled.TryGetValue(entry.Input.Value, out var kept))
            {
                var input = source.Inputs.FirstOrDefault(i => i.Recorded.Input == entry.Input);
                if (input is null)
                {
                    return WorkflowErrors.InjectionChanged.With($"the plan names input {entry.Input.Value}, which run {source.Run} did not fill (fix: re-run the Step)");
                }

                var loaded = await InputRefills.Texts(texts, runner, address, source.State, source.Run, input with { Context = input.Context with { Scope = scope } }, WorkflowErrors.InjectionChanged, cancellationToken).ConfigureAwait(false);
                if (loaded.IsError)
                {
                    return loaded.Error;
                }

                kept = loaded.Value;
                filled[entry.Input.Value] = kept;
            }

            if (!kept.Sections.TryGetValue(entry.Section, out var text))
            {
                return WorkflowErrors.InjectionChanged.With($"input {entry.Input.Value} no longer fills section '{entry.Section}' (fix: re-run the Step)");
            }

            sections.Add(new InjectionSectionView(entry.Section, entry.Input.Value, entry.Bytes, entry.Sha256, text));
        }

        return new InjectionView(source.Run, sections);
    }
}
