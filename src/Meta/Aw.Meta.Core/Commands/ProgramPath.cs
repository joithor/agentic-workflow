namespace Aw.Meta.Core.Commands;

/// <summary>Where a command's program is looked up: a relative path with a directory part belongs to the command's working directory.</summary>
public static class ProgramPath
{
    /// <summary>
    /// The program to start. A relative path with a '/' in it (scripts/check.sh) is resolved against the working directory the command
    /// runs in, because the OS resolves it against the parent's directory otherwise. A bare name is left to PATH.
    /// </summary>
    /// <param name="program">The first word of the command.</param>
    /// <param name="cwd">The directory the command runs in, or null when it inherits ours.</param>
    /// <returns>The program to start.</returns>
    public static string Resolve(string program, string? cwd)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (cwd is not { Length: > 0 } || !program.Contains('/', StringComparison.Ordinal) || program.StartsWith('/'))
        {
            return program;
        }

        var relative = program;
        while (relative.StartsWith("./", StringComparison.Ordinal))
        {
            relative = relative[2..];
        }

        return cwd.TrimEnd('/') + "/" + relative;
    }
}
