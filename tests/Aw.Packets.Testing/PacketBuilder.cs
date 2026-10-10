using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Testing;

/// <summary>
/// Builds a packet. The default passes every implemented check against <see cref="BaseFiles"/> at <see cref="BaseSha"/>:
/// one workstream, one task writing 'src/**' with a 'test' example and a held-out 'cli-transcript' example, one
/// answered question.
/// </summary>
internal sealed class PacketBuilder
{
    /// <summary>The default base commit.</summary>
    public const string BaseSha = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>The default workstream.</summary>
    public const string Workstream = "ws1";

    private readonly List<PacketTask> _tasks = [];
    private readonly List<Question> _questions = [];
    private string _id = "demo-packet";
    private string _baseSha = BaseSha;

    /// <summary>The files at <see cref="BaseSha"/> in the default repository.</summary>
    public static IReadOnlySet<string> BaseFiles { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "README.md", "src/a.cs", "src/b.cs", "docs/guide.md",
    };

    /// <summary>Sets the packet id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>This builder.</returns>
    public PacketBuilder Id(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>Sets the base commit.</summary>
    /// <param name="sha">The sha.</param>
    /// <returns>This builder.</returns>
    public PacketBuilder Base(string sha)
    {
        _baseSha = sha;
        return this;
    }

    /// <summary>Adds a task (the default task is used only when none is added).</summary>
    /// <param name="task">The task.</param>
    /// <returns>This builder.</returns>
    public PacketBuilder With(TaskBuilder task)
    {
        _tasks.Add(task.Build());
        return this;
    }

    /// <summary>Adds a question (the default answered one is used only when none is added).</summary>
    /// <param name="question">The question.</param>
    /// <returns>This builder.</returns>
    public PacketBuilder With(Question question)
    {
        _questions.Add(question);
        return this;
    }

    /// <summary>Builds the packet.</summary>
    /// <returns>The packet.</returns>
    public Packet Build() => new(
        new PacketManifest
        {
            SchemaVersion = 1,
            Id = PacketId.From(_id),
            Repo = "example/repo",
            BaseSha = _baseSha,
            Status = PacketStatus.Draft,
            Links = new PacketLinks
            {
                Decisions = "decisions.json",
                OutOfScope = "out-of-scope.json",
                Tasks = "tasks.json",
                Design = ["design/design.md"],
                Plans = [],
            },
        },
        new DecisionLog(1, _questions.Count > 0 ? _questions : [new Question { Id = QuestionId.From("q1"), Text = "Which shape?", Answer = "small" }]),
        new OutOfScope(1, ["anything else"]),
        new TaskPlan(
            1,
            [new Workstream(WorkstreamId.From(Workstream), "the work")],
            _tasks.Count > 0 ? _tasks : [new TaskBuilder("t1").Example("t1-e1", holdout: false, "test").Example("t1-h1", holdout: true, "cli-transcript").Build()]));
}
