using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// 'step.prove.echo', the test-only block Step a hand test drives (with 'input.prove.echo'; registered only under the prove gate): it
/// logs one line per section and per typed result it was given, 'step &lt;id&gt; &lt;name&gt; &lt;sha12&gt;', which never holds the text, and
/// completes 'done'. That is what shows a block Step receiving its injection through the block runner.
/// </summary>
/// <param name="echo">The call log.</param>
internal sealed class ProveEchoStep(IProveEcho echo) : IBlockStep, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "step.prove.echo";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From(Id), BlockKind.Step, Version: 1, Inputs: [], Outputs: [], Outcomes: [OutcomeName.From("done")], Capability.None, Parameters: [],
        Rung: null, DeskCapable: false, Runtime: BlockRuntime.Block);

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var (name, text) in context.Injection.Sections.OrderBy(static s => s.Key, StringComparer.Ordinal))
        {
            await echo.Log($"step {Id} {name} {Sha12(text)}", cancellationToken).ConfigureAwait(false);
        }

        foreach (var (input, result) in context.InputResults.OrderBy(static r => r.Key, StringComparer.Ordinal))
        {
            await echo.Log($"step {Id} result {input} {Sha12(result.GetRawText())}", cancellationToken).ConfigureAwait(false);
        }

        return BlockStepResult.Completed(OutcomeName.From("done"), new Dictionary<FactKey, System.Text.Json.JsonElement>());
    }

    private static string Sha12(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];
}
