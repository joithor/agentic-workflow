using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Testing;

/// <summary>The in-memory IJudgeConfigStore fake; it passes JudgeConfigStoreContract.</summary>
public sealed class InMemoryJudgeConfigStore : IJudgeConfigStore
{
    private static readonly Error _unavailable = new("AW-DATA-002", ErrorKind.Unavailable, "the in-memory config store is set unavailable");

    /// <summary>When true every write returns an error, as a config directory that can't be created.</summary>
    public bool Unavailable { get; init; }

    /// <summary>The stored config, or null when nothing was saved.</summary>
    public JudgeConfig? Stored { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<JudgeConfig>> Load(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(Stored ?? JudgeConfig.Default));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Save(JudgeConfig config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (Unavailable)
        {
            return ValueTask.FromResult(Results.Failure<Unit>(_unavailable));
        }

        Stored = config;
        return ValueTask.FromResult(Results.Ok());
    }
}
