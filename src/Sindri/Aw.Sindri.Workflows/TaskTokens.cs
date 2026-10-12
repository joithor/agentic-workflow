namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The run-token-to-task-token map (engine spec rev 8 §5.3; spec: the API stores the task token on 'step.started' in
/// the ledger). Engine P4a: one small file per open desk run under the Sindri home, so the CLI process finds the
/// task token the worker process recorded, and a worker restart loses nothing (a replay never re-runs RunStep, so an
/// in-memory map would be empty after a restart).
/// </summary>
/// <param name="home">The adapter's home.</param>
internal sealed class TaskTokens(WorkflowsHome home)
{
    /// <summary>Records <paramref name="taskToken"/> for run <paramref name="run"/> of <paramref name="address"/>, replacing an earlier one.</summary>
    public void Save(string address, string run, byte[] taskToken)
    {
        var file = PathOf(address, run);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!); // why: PathOf always has a directory
        var temp = file + ".tmp";
        File.WriteAllText(temp, Convert.ToBase64String(taskToken));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(temp, file, overwrite: true);
    }

    /// <summary>The task token recorded for the run, or null when no desk run with that token is open.</summary>
    public byte[]? Find(string address, string run)
    {
        var file = PathOf(address, run);
        return File.Exists(file) ? Convert.FromBase64String(File.ReadAllText(file).Trim()) : null;
    }

    /// <summary>Forgets the run once its report completed it.</summary>
    public void Remove(string address, string run) => File.Delete(PathOf(address, run));

    private string PathOf(string address, string run) =>
        Path.Combine(home.TasksDirectory, address.Replace('/', '~'), run);
}
