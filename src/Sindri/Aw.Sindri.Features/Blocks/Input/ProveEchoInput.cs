using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Input;

/// <summary>
/// 'input.prove.echo', the test-only Step input a proof drives (plan decision 9, engine Q2): it serves the text of a fixed file under the
/// prover home as the section named by its 'section' parameter, and appends one line per call to a fixed log, 'fill &lt;input&gt; &lt;step&gt;
/// &lt;sha12&gt;', which never holds the text. 'mode: throw' makes it fail after logging. It is registered only under AW_SINDRI_PROVE_BLOCKS=1 in a
/// prover home, so a normal build has it in neither the catalog nor DI.
/// </summary>
/// <param name="echo">The files behind it.</param>
internal sealed class ProveEchoInput(IProveEcho echo) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.prove.echo";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From(Id), BlockKind.StepInput, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.None,
        [new ParameterSpec("section", ParameterType.Text, Required: true, IsCapabilityField: false), new ParameterSpec("mode", ParameterType.Text, Required: false, IsCapabilityField: false)],
        Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public async ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var text = await echo.ReadText(cancellationToken).ConfigureAwait(false);
        var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];
        await echo.Log($"fill {context.Input.Value} {context.Step.Value} {sha}", cancellationToken).ConfigureAwait(false);
        if (Argument(context, "mode") == "throw")
        {
            throw new InvalidOperationException("the echo input was told to throw");
        }

        return new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { [Argument(context, "section") ?? "echo"] = text });
    }

    private static string? Argument(StepInputContext context, string name) =>
        context.Arguments.TryGetValue(name, out var value) ? value.GetString() : null;
}
