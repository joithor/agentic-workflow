using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'step.pr.ci-log' on the block runtime (PB spec §10.1, CI-red autofix): reads the failing checks from 'fact:prState' (checks.failing) and keeps a capped excerpt of each
/// one's log, as 'fact:ciLog' { head, checks: [{ name, excerpt }] }. The forge scrubs and caps the log it returns; the excerpt is kept here as data, cut again to
/// <see cref="ExcerptLimit"/> characters from the end (the failure is at the tail), and never becomes a line of any prompt. 'logged' when a failing check was read, 'none'
/// when the snapshot lists no failing check. A forge error fails the Step with its SND-FORGE code.
/// </summary>
/// <param name="pulls">The forge.</param>
internal sealed class PrCiLogStep(IPullRequests pulls) : IBlockStep, IBlockStepId
{
    /// <summary>The most characters of one check's log kept in the fact.</summary>
    internal const int ExcerptLimit = 8000;

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.ci-log");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Read(context, "fact:prState") is not JsonObject state || Link(context, state) is not { } link)
        {
            return BlockStepResult.Failed("step.pr.ci-log needs fact:prState (with its link) or the 'pr' argument");
        }

        var checks = new JsonArray();
        foreach (var name in FailingChecks(state))
        {
            var log = await pulls.CheckLog(link, name, cancellationToken).ConfigureAwait(false);
            if (log.IsError)
            {
                return BlockStepResult.Failed($"{log.Error.Code}: step.pr.ci-log can't read the log of check {name}: {log.Error.Message}");
            }

            var excerpt = log.Value.Length > ExcerptLimit ? log.Value[^ExcerptLimit..] : log.Value;
            checks.Add((JsonNode)new JsonObject { ["name"] = name, ["excerpt"] = excerpt });
        }

        return StepFacts.Done(checks.Count == 0 ? "none" : "logged", ("fact:ciLog", new JsonObject { ["head"] = StepFacts.Field(state["head"], "sha") ?? StepFacts.Field(state, "headSha"), ["checks"] = checks }));
    }

    private static IEnumerable<string> FailingChecks(JsonObject state) =>
        (state["checks"]?["failing"] as JsonArray ?? []).Select(static n => n is JsonValue v && v.TryGetValue<string>(out var name) ? name : null).OfType<string>().Where(static n => n.Length > 0).Distinct(StringComparer.Ordinal);

    private static string? Link(StepContext context, JsonObject state) =>
        context.Arguments.TryGetValue("pr", out var argument) && argument.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(argument.GetString())
            ? argument.GetString()
            : StepFacts.Field(state, "link");
}
