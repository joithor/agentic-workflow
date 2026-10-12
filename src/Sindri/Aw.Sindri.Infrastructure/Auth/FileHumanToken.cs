using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Auth;

namespace Aw.Sindri.Infrastructure.Auth;

/// <summary>
/// The per-start human token as &lt;home&gt;/auth/human.token (API spec §6): 32 random bytes in base64url, written
/// atomically as a 0600 file in a 0700 'auth' folder. Only 'serve' mints and removes it; the CLI and the app read it.
/// </summary>
/// <param name="settings">The settings naming the token file.</param>
internal sealed class FileHumanToken(SindriSettings settings) : IHumanTokenStore
{
    private const int Bytes = 32;
    private readonly string _folder = Path.GetDirectoryName(settings.TokenPath) ?? settings.Home;

    public ValueTask<Result<HumanToken>> Mint(CancellationToken cancellationToken)
    {
        var token = new HumanToken(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(Bytes)));
        try
        {
            OwnerOnly.EnsureDirectory(_folder);
            OwnerOnly.WriteAtomically(settings.TokenPath, Encoding.UTF8.GetBytes(token.Value + "\n"));
            return ValueTask.FromResult<Result<HumanToken>>(token);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<HumanToken>>(Unwritable(e));
        }
    }

    public async ValueTask<Result<HumanToken?>> Read(CancellationToken cancellationToken)
    {
        try
        {
            var text = (await File.ReadAllTextAsync(settings.TokenPath, cancellationToken).ConfigureAwait(false)).Trim();
            return text.Length == 0 ? Results.Success<HumanToken?>(value: null) : new HumanToken(text);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return Results.Success<HumanToken?>(value: null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Unwritable(e);
        }
    }

    public ValueTask<Result<Unit>> Remove(CancellationToken cancellationToken)
    {
        try
        {
            File.Delete(settings.TokenPath);   // no error when the file is missing
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
        catch (DirectoryNotFoundException)
        {
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<Unit>>(Unwritable(e));
        }
    }

    private Error Unwritable(Exception e) => SindriErrors.LedgerUnavailable.With(
        $"{settings.TokenPath} can't be read or written: {e.Message} (fix: check the permissions of {_folder})");
}
