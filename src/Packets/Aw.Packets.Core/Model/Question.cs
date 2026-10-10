using Aw.Packets.Core.Ids;

namespace Aw.Packets.Core.Model;

/// <summary>One decision-log entry: a question, answered or deferred.</summary>
public sealed record Question
{
    /// <summary>The question id.</summary>
    public required QuestionId Id { get; init; }

    /// <summary>The question.</summary>
    public required string Text { get; init; }

    /// <summary>Who asked it.</summary>
    public string? Asker { get; init; }

    /// <summary>The answer, once answered.</summary>
    public string? Answer { get; init; }

    /// <summary>The deferral, when it is deferred instead.</summary>
    public Deferral? Deferred { get; init; }

    /// <summary>The product nodes the answer affects.</summary>
    public IReadOnlyList<string> Affects { get; init; } = [];
}
