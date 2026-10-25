namespace Aw.Sindri.Core.Scheduling;

/// <summary>What the gate says about a held map: <see cref="MapWait"/>, <see cref="MapPublish"/> or <see cref="MapDrop"/>.</summary>
public abstract record MapPublishAction
{
    private protected MapPublishAction()
    {
    }
}
