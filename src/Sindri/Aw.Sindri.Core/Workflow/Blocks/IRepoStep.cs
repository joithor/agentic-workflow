namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>Marks a block-runtime Step that reads <see cref="StepContext.Repo"/>: the runner resolves the instance's repo (the ledger's, for a child) only for such a Step, a Step with Step inputs, or one that takes 'providers'.</summary>
public interface IRepoStep;
