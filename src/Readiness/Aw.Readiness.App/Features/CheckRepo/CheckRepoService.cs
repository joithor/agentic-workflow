using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Readiness.Core;
using Aw.Readiness.Core.Criteria;
using Aw.Readiness.Core.Ports;
using Aw.Readiness.Core.Scoring;

namespace Aw.Readiness.App.Features.CheckRepo;

/// <summary>Checks a checkout against its ring's target: reads readiness.yaml, asks the host (unless offline),
/// evaluates the criteria, scores them, diffs against the previous run and records this one.</summary>
internal sealed class CheckRepoService(IRepoReader reader, IRepoHost host, IRunHistory history, TimeProvider clock)
    : IService<CheckRepoRequest, CheckRepoResponse>
{
    public async ValueTask<Result<CheckRepoResponse>> Handle(CheckRepoRequest request, CancellationToken cancellationToken)
    {
        var tree = reader.Open(request.Root);
        if (tree.IsError)
        {
            return tree.Error;
        }

        var config = ReadConfig(tree.Value);
        if (config.IsError)
        {
            return config.Error;
        }

        var facts = request.UseHost
            ? await host.Read(request.Root, cancellationToken).ConfigureAwait(false)
            : HostFacts.Unknown("not read: --no-remote");
        var now = clock.GetUtcNow();
        var findings = TemplateReadiness.Evaluate(tree.Value, facts, now);
        return await Record(config.Value, findings, now, cancellationToken).ConfigureAwait(false);
    }

    private static Result<ReadinessConfig> ReadConfig(IRepoTree tree) => tree.ReadText(ReadinessConfig.FileName) is { } text
        ? ReadinessConfig.Parse(text)
        : ReadinessErrors.ConfigInvalid.With($"no {ReadinessConfig.FileName} at the repo root; fix: add one (docs/readiness/README.md)");

    private async Task<Result<CheckRepoResponse>> Record(
        ReadinessConfig config, IReadOnlyList<Finding> findings, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var previous = await history.Latest(config.Repo, config.Scope, cancellationToken).ConfigureAwait(false);
        if (previous.IsError)
        {
            return previous.Error;
        }

        var card = ReadinessScoring.Score(findings);
        var run = RunRecord.From(config.Repo, config.Scope, now, card.Level, findings);
        var unconfirmed = TemplateReadiness.Unconfirmed(findings, config.Target);
        var saved = await history.Save(run, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        return new CheckRepoResponse(
            CheckRepoResponse.CurrentSchemaVersion, config.Repo, config.Scope, now, config.Target, card.Level,
            card.Level >= config.Target.Level && unconfirmed.Count == 0, unconfirmed, saved.Value,
            RunDelta.Between(previous.Value, run), card.Pillars, card.Tallies, findings);
    }
}
