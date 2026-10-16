using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// IOnyxWorkspace over the file system under the Sindri home: every write goes to a temporary file in the target's directory and is renamed over it, so a reader sees
/// the old or the new content and a crash leaves no half file. A protected file is created 0600 from the start (no window where it is world-readable); the state is 0600.
/// </summary>
/// <param name="home">The Sindri home the onyx directory sits in.</param>
/// <param name="json">The AwJson options the state is written with.</param>
internal sealed class FileOnyxWorkspace(ISindriHome home, JsonSerializerOptions json) : IOnyxWorkspace
{
    private const UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly OnyxPaths _paths = OnyxPaths.For(home.Path);

    /// <inheritdoc />
    public string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    /// <inheritdoc />
    public bool WriteIfChanged(string path, string content, bool secret)
    {
        if (Read(path) is { } have && string.Equals(have, content, StringComparison.Ordinal))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(path)!;   // why: every path Sindri passes has a directory
        if (secret)
        {
            OwnerOnly.EnsureDirectory(directory);
        }
        else
        {
            _ = Directory.CreateDirectory(directory);
        }

        WriteAtomically(path, Encoding.UTF8.GetBytes(content), secret);
        return true;
    }

    /// <inheritdoc />
    public OnyxSetupState? ReadState()
    {
        try
        {
            var path = _paths.State;
            return File.Exists(path) && AwJson.Deserialize<OnyxSetupState>(File.ReadAllBytes(path), json) is { IsSuccess: true } read ? read.Value : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void WriteState(OnyxSetupState state)
    {
        OwnerOnly.EnsureDirectory(_paths.Root);
        WriteAtomically(_paths.State, AwJson.Serialize(state, json), secret: true);
    }

    /// <inheritdoc />
    public IDisposable? TryLock()
    {
        OwnerOnly.EnsureDirectory(_paths.Root);
        try
        {
            return new FileStream(_paths.Root + "/.lock", new FileStreamOptions { Mode = FileMode.OpenOrCreate, Access = FileAccess.ReadWrite, Share = FileShare.None });
        }
        catch (IOException)
        {
            return null;   // another `onyx up` holds it
        }
    }

    private static void WriteAtomically(string path, byte[] bytes, bool secret)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (secret && !OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerOnlyFile;
        }

        try
        {
            using (var stream = new FileStream(temporary, options))
            {
                stream.Write(bytes);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
