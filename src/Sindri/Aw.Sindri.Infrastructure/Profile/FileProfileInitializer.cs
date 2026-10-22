using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary><see cref="IProfileInitializer"/> over the live profile file: created owner-only, and never over a file that is there (the create fails if another process wrote one first).</summary>
/// <param name="settings">Where the profile is.</param>
internal sealed class FileProfileInitializer(SindriSettings settings) : IProfileInitializer
{
    public ValueTask<Result<string>> Create(IReadOnlyCollection<string> defaultEffects, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(defaultEffects);
        cancellationToken.ThrowIfCancellationRequested();
        var path = settings.ProfilePath;
        var exists = SindriErrors.ProfileInvalid.With($"a profile already exists at {path} (fix: edit it, or remove it first)");
        if (File.Exists(path))
        {
            return ValueTask.FromResult<Result<string>>(exists);
        }

        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
            {
                OwnerOnly.EnsureDirectory(directory);
            }

            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(DefaultProfile.Text(Environment.UserName, defaultEffects)));
            }

            OwnerOnly.RestrictFiles(path);
            return ValueTask.FromResult<Result<string>>(path);
        }
        catch (IOException) when (File.Exists(path))
        {
            return ValueTask.FromResult<Result<string>>(exists);   // another process created it between the check and the create
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<string>>(SindriErrors.ProfileInvalid.With($"{path} can't be written: {e.Message} (fix: make the folder writable, then run 'sindri profile init' again)"));
        }
    }
}
