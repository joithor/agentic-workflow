using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Testing;

/// <summary>The process table in memory: scripted processes with their raw environments, which it reduces to the allowlist as the real adapter does. Kept honest by <see cref="ProcessTableContract"/>.</summary>
public sealed class FakeProcessTable : IProcessTable
{
    private readonly Dictionary<int, ProcessEntry> _table = [];
    private readonly Dictionary<int, IReadOnlyDictionary<string, string>> _environments = [];
    private readonly Dictionary<int, string> _cwds = [];
    private readonly Dictionary<int, IReadOnlyList<string>> _files = [];
    private string? _failure;

    /// <summary>Adds a process.</summary>
    /// <param name="process">The process.</param>
    /// <param name="environment">Its whole environment (the allowlist is applied when it is read).</param>
    /// <param name="cwd">Its working directory.</param>
    /// <param name="files">Its open file targets.</param>
    /// <returns>This table.</returns>
    public FakeProcessTable With(ProcessEntry process, IReadOnlyDictionary<string, string>? environment = null, string? cwd = null, IReadOnlyList<string>? files = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        _table[process.Pid] = process;
        _environments[process.Pid] = environment ?? new Dictionary<string, string>(StringComparer.Ordinal);
        if (cwd is not null)
        {
            _cwds[process.Pid] = cwd;
        }

        _files[process.Pid] = files ?? [];
        return this;
    }

    /// <summary>Removes a process, as its exit does.</summary>
    /// <param name="pid">The process id.</param>
    public void Exit(int pid) => _ = _table.Remove(pid);

    /// <summary>Makes <see cref="Read"/> fail as an unreadable table does.</summary>
    /// <param name="detail">'proc-unreadable' or 'unsupported-platform'.</param>
    public void Fail(string detail = "proc-unreadable") => _failure = detail;

    /// <summary>Makes <see cref="Read"/> work again.</summary>
    public void Heal() => _failure = null;

    /// <inheritdoc/>
    public Result<IReadOnlyList<ProcessEntry>> Read(CancellationToken cancellationToken) =>
        _failure is null ? Results.Success<IReadOnlyList<ProcessEntry>>([.. _table.Values.OrderBy(static p => p.Pid)]) : SindriErrors.SessionsUnavailable.With(_failure);

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Environment(int pid) =>
        _environments.TryGetValue(pid, out var all) ? EnvironmentAllowlist.Filter(all) : new Dictionary<string, string>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string? Cwd(int pid) => _cwds.GetValueOrDefault(pid);

    /// <inheritdoc/>
    public IReadOnlyList<string> OpenFiles(int pid) => _files.GetValueOrDefault(pid) ?? [];
}
