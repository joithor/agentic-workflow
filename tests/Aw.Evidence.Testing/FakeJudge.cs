using Aw.Evidence.Core.Judging;

namespace Aw.Evidence.Testing;

/// <summary>A scripted IJudge: answers from queues (null when empty: unavailable) and records every request.</summary>
public sealed class FakeJudge : IJudge
{
    private readonly Queue<SelectorRepair?> _repairs = new();
    private readonly Queue<VisualVerdict?> _verdicts = new();
    private readonly List<SelectorRepairRequest> _repairRequests = [];
    private readonly List<VisualCritiqueRequest> _critiqueRequests = [];

    /// <summary>A judge that is always unavailable.</summary>
    public static FakeJudge Unavailable => new();

    /// <summary>The repair requests received.</summary>
    public IReadOnlyList<SelectorRepairRequest> RepairRequests => _repairRequests;

    /// <summary>The critique requests received.</summary>
    public IReadOnlyList<VisualCritiqueRequest> CritiqueRequests => _critiqueRequests;

    /// <summary>Queues repair answers (null answers as unavailable).</summary>
    /// <param name="answers">The answers, in order.</param>
    /// <returns>This judge.</returns>
    public FakeJudge Repairs(params SelectorRepair?[] answers)
    {
        foreach (var answer in answers)
        {
            _repairs.Enqueue(answer);
        }

        return this;
    }

    /// <summary>Queues critique answers (null answers as unavailable).</summary>
    /// <param name="answers">The answers, in order.</param>
    /// <returns>This judge.</returns>
    public FakeJudge Critiques(params VisualVerdict?[] answers)
    {
        foreach (var answer in answers)
        {
            _verdicts.Enqueue(answer);
        }

        return this;
    }

    /// <inheritdoc/>
    public ValueTask<SelectorRepair?> RepairSelectorAsync(SelectorRepairRequest request, CancellationToken cancellationToken)
    {
        _repairRequests.Add(request);
        return ValueTask.FromResult(_repairs.Count > 0 ? _repairs.Dequeue() : null);
    }

    /// <inheritdoc/>
    public ValueTask<VisualVerdict?> CritiqueAsync(VisualCritiqueRequest request, CancellationToken cancellationToken)
    {
        _critiqueRequests.Add(request);
        return ValueTask.FromResult(_verdicts.Count > 0 ? _verdicts.Dequeue() : null);
    }
}
