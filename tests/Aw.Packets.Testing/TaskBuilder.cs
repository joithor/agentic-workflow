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
        _examples.Add(new AcceptanceExample { Id = ExampleId.From(exampleId), Text = $"{exampleId} holds", Evidence = evidence, Holdout = holdout });
        return this;
    }

    /// <summary>Builds the task (one '&lt;id&gt;-e1' 'test' example when none was added).</summary>
    /// <returns>The task.</returns>
    public PacketTask Build() => new()
    {
        Id = TaskId.From(id),
        Workstream = WorkstreamId.From(_workstream),
        Title = $"task {id}",
        Deps = [.. _deps.Select(TaskId.From)],
        Size = SizeClass.Small,
        WriteScope = _scope,
        ReadSeeds = ["README.md"],
        Acceptance = _examples.Count > 0
            ? _examples
            : [new AcceptanceExample { Id = ExampleId.From($"{id}-e1"), Text = $"{id} works", Evidence = ["test"] }],
        Budget = new TaskBudget { Tokens = 100_000, Turns = 40, WallMinutes = 60 },
    };
}
