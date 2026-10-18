using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Testing;

/// <summary>Builds a <see cref="PacketTask"/>.</summary>
/// <param name="id">The task id.</param>
internal sealed class TaskBuilder(string id)
{
    private readonly List<string> _deps = [];
    private readonly List<AcceptanceExample> _examples = [];
    private string[] _scope = ["src/**"];
    private string _workstream = PacketBuilder.Workstream;
    private bool _none;
    private SizeClass _size = SizeClass.Small;
    private TaskBudget _budget = new() { Tokens = 100_000, Turns = 40, WallMinutes = 60 };

    /// <summary>Sets the size class (small by default).</summary>
    /// <param name="size">The size.</param>
    /// <returns>This builder.</returns>
    public TaskBuilder Sized(SizeClass size)
    {
        _size = size;
        return this;
    }

    /// <summary>Sets the budget (100k tokens, 40 turns, 60 minutes by default).</summary>
    /// <param name="budget">The budget.</param>
    /// <returns>This builder.</returns>
    public TaskBuilder Budgeted(TaskBudget budget)
    {
        _budget = budget;
        return this;
    }

    /// <summary>Adds dependencies.</summary>
    /// <param name="deps">Task ids it waits for.</param>
    /// <returns>This builder.</returns>
    public TaskBuilder After(params string[] deps)
    {
        _deps.AddRange(deps);
        return this;
    }

    /// <summary>Replaces the write scope.</summary>
    /// <param name="scope">Globs or 'new: path' entries.</param>
    /// <returns>This builder.</returns>
    public TaskBuilder Writing(params string[] scope)
    {
        _scope = scope;
        return this;
    }

    /// <summary>Moves the task to another workstream.</summary>
    /// <param name="workstream">The workstream id.</param>
    /// <returns>This builder.</returns>
    public TaskBuilder In(string workstream)
    {
        _workstream = workstream;
        return this;
    }

    /// <summary>Adds an acceptance example.</summary>
    /// <param name="exampleId">The example id.</param>
    /// <param name="holdout">Whether it is held out.</param>
    /// <param name="evidence">Its evidence kinds.</param>
    /// <returns>This builder.</returns>
    public TaskBuilder Example(string exampleId, bool holdout, params string[] evidence)
    {
        _examples.Add(new AcceptanceExample { Id = ExampleId.From(exampleId), Text = holdout ? null : $"{exampleId} holds", Evidence = evidence, Holdout = holdout });
        return this;
    }

    /// <summary>Adds an acceptance example with exactly the given text (null for none), held out or not.</summary>
    /// <param name="exampleId">The example id.</param>
    /// <param name="holdout">Whether it is held out.</param>
    /// <param name="text">Its text in the packet.</param>
    /// <returns>This builder.</returns>
    public TaskBuilder ExampleWithText(string exampleId, bool holdout, string? text)
    {
        _examples.Add(new AcceptanceExample { Id = ExampleId.From(exampleId), Text = text, Evidence = ["test"], Holdout = holdout });
        return this;
    }

    /// <summary>Leaves the task with no acceptance examples at all.</summary>
    /// <returns>This builder.</returns>
    public TaskBuilder NoExamples()
    {
        _none = true;
        return this;
    }

    /// <summary>Builds the task (a visible '&lt;id&gt;-e1' and a held-out '&lt;id&gt;-h1' 'test' example when none was added).</summary>
    /// <returns>The task.</returns>
    public PacketTask Build() => new()
    {
        Id = TaskId.From(id),
        Workstream = WorkstreamId.From(_workstream),
        Title = $"task {id}",
        Deps = [.. _deps.Select(TaskId.From)],
        Size = _size,
        WriteScope = _scope,
        ReadSeeds = ["README.md"],
        Acceptance = _none ? [] : _examples.Count > 0
            ? _examples
            :
            [
                new AcceptanceExample { Id = ExampleId.From($"{id}-e1"), Text = $"{id} works", Evidence = ["test"] },
                new AcceptanceExample { Id = ExampleId.From($"{id}-h1"), Evidence = ["test"], Holdout = true },
            ],
        Budget = _budget,
    };
}
