using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// An agent CLI run headless on a small fast model: each question gets a fresh empty temp directory (deleted
/// afterwards) and the recursion guard AW_JUDGE_CHILD=1, which makes every aw:* hook in the child exit at once. The
/// subclass says how to call its CLI and how to read its output; the run's end maps the same way for all of them.
/// </summary>
/// <param name="runner">Runs the CLI.</param>
/// <param name="cli">Which agent CLI this is.</param>
internal abstract class AgentCliProvider(IProcessRunner runner, AgentCliName cli) : IModelProvider
{
    /// <inheritdoc />
    public ProviderName Name => AgentCliNames.Provider(cli);

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass>(ContentClasses.All);

    /// <inheritdoc />
    public async ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        using var workspace = TempWorkspace.Create();
        AgentCliCall call;
        if (question.ContentClass == ContentClass.Image)
        {
            if (Evidence(input) is not { } evidence)
            {
                return new ProviderErrored(ProviderReasons.InvalidImageInput);
            }

            call = ForImage(question, evidence, workspace.Path);
        }
        else
        {
            call = ForText(question, input, workspace.Path);
        }

        var result = await runner.Run(Request(call, budget), cancellationToken).ConfigureAwait(false);
        return result.End switch
        {
            ProcessEnd.NotStarted => new ProviderUnavailable(ProviderReasons.NotInstalled),
            ProcessEnd.TimedOut => new ProviderUnavailable(ProviderUnavailable.Timeout),
            ProcessEnd.Exited when result.ExitCode != 0 => new ProviderErrored(ProviderReasons.Exit(result.ExitCode)),
            ProcessEnd.Exited => Read(result.StandardOutput, question),
            _ => throw new InvalidOperationException($"unknown process end {result.End}"),
        };
    }

    /// <summary>The call for a text question, run in the fresh workspace.</summary>
    /// <param name="question">The question.</param>
    /// <param name="input">The input.</param>
    /// <param name="workspace">The fresh empty directory.</param>
    /// <returns>The call.</returns>
    protected abstract AgentCliCall ForText(QuestionRef question, JsonNode? input, string workspace);

    /// <summary>The call for an image question.</summary>
    /// <param name="question">The question.</param>
    /// <param name="evidence">The screenshots.</param>
    /// <param name="workspace">The fresh empty directory.</param>
    /// <returns>The call.</returns>
    protected abstract AgentCliCall ForImage(QuestionRef question, ImageEvidence evidence, string workspace);

    /// <summary>Reads the CLI's standard output (it exited 0).</summary>
    /// <param name="output">The output.</param>
    /// <param name="question">The question.</param>
    /// <returns>Decided, or errored with why.</returns>
    protected abstract ProviderResult Read(string output, QuestionRef question);

    private static ImageEvidence? Evidence(JsonNode? input) =>
        input is JsonObject ? WireJson.Read<ImageEvidence>(WireJson.Text(input)) : null;

    private ProcessRequest Request(AgentCliCall call, TimeSpan budget) => new()
    {
        FileName = AgentCliNames.Binary(cli),
        Arguments = call.Arguments,
        WorkingDirectory = call.WorkingDirectory,
        Environment = new Dictionary<string, string>(call.Environment, StringComparer.Ordinal) { ["AW_JUDGE_CHILD"] = "1" },
        Timeout = budget,
    };
}
