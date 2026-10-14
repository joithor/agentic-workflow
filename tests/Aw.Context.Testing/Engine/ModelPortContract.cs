using Aw.Context.Core.Models;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract every <see cref="IModelPort"/> honours (spec §4, README X18), run against the fake and each adapter a
/// later stage adds: an answer within the request's output cap, token counts reported, and cancellation honoured.
/// </summary>
/// <typeparam name="TPort">The implementation.</typeparam>
public abstract class ModelPortContract<TPort>
    where TPort : IModelPort
{
    private static readonly ModelRequest _request = new(ModelPurpose.Labeling, "Name this feature: split pane orientation.", MaxTokens: 50, TimeSpan.FromSeconds(30));

    /// <summary>A fresh implementation that can answer.</summary>
    /// <returns>The port.</returns>
    protected abstract TPort Create();

    /// <summary>An answer has text, stays within the output cap and reports the tokens it cost.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_answer_stays_within_its_cap_and_reports_its_tokens()
    {
        var answer = await Create().Ask(_request, CancellationToken.None);

        Check(answer.IsSuccess, "a simple request is answered");
        Check(answer.Value.OutputTokens <= _request.MaxTokens && answer.Value.InputTokens > 0, "tokens are reported and within the cap");
    }

    /// <summary>A cancelled call throws OperationCanceledException, never an error result.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Cancellation_is_honoured()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var thrown = await Record.ExceptionAsync(async () => await Create().Ask(_request, cancelled.Token));

        Check(thrown is OperationCanceledException, "a cancelled call throws OperationCanceledException");
    }

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TPort).Name}: {what}");
        }
    }
}
