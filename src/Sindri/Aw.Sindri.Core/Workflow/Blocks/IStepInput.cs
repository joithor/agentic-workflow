using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A Step input block (spec §2): fills sections of a Step's injection plan before it starts, for example 'input.context.retrieve'. Declared in P1; its first runtime comes with the context engine.</summary>
public interface IStepInput
{
    /// <summary>Fills the sections.</summary>
    /// <param name="context">What it fills from.</param>
    /// <param name="cancellationToken">Cancels the fill.</param>
    /// <returns>The sections it filled; an error when the fill must fail (a refill that finds its data changed is SND-WF-018), never an exception for an expected failure.</returns>
    public ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken);
}
