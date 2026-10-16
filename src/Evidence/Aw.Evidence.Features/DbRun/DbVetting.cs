using System.Text;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Kernel;

namespace Aw.Evidence.Features.DbRun;

/// <summary>The guards that run before any lock: the write statements, the database target, the goldens and the seed.</summary>
internal sealed class DbVetting(DataRunFiles files, IEvidenceTargets targets, IExecutableLocator locator, ISecretsSource secrets)
{
    internal const string WorkFolder = "db";

    internal async ValueTask<Result<VettedDb>> VetAsync(DbRunRequest request, DbScript script, CancellationToken cancellationToken)
    {
        for (var i = 0; i < script.QueryList.Count; i++)
        {
            var allowed = WriteStatements.Check(script.QueryList[i].Sql);
            if (allowed.IsError)
            {
                return allowed.Error with { Message = $"query {(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}: {allowed.Error.Message}" };
            }
        }

        var goldens = new List<string?>();
        foreach (var query in script.QueryList)
        {
            var golden = await ReadTextAsync(request.ScriptPath, query.Expect.Golden, cancellationToken).ConfigureAwait(false);
            if (golden.IsError)
            {
                return golden.Error;
            }

            goldens.Add(golden.Value);
        }

        var seed = await ReadTextAsync(request.ScriptPath, script.Seed, cancellationToken).ConfigureAwait(false);
        if (seed.IsError)
        {
            return seed.Error;
        }

        var target = script.Connection is { } name
            ? await PostgresAsync(request, name, cancellationToken).ConfigureAwait(false)
            : Sqlite(request, script, seed.Value);
        return target.Map(t => new VettedDb(script, t, goldens, seed.Value is null ? null : ContentHash.Sha256Hex(Encoding.UTF8.GetBytes(seed.Value)), t is PostgresTarget p ? [p.Password] : []));
    }

    private Result<DbTarget> Sqlite(DbRunRequest request, DbScript script, string? seedSql)
    {
        var given = script.Sqlite ?? string.Empty;
        var path = given.StartsWith('/') ? given : RunPath.Combine(request.Repo, given);
        var real = locator.RealFile(path);
        if (real is null)
        {
            return EvidenceErrors.DatabaseUnavailable.With($"{path} is not a file");
        }

        var roots = new[] { locator.RealDirectory(request.Repo), locator.RealDirectory(request.RunDirectory) };
        if (!roots.Any(root => root is not null && real.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal)))
        {
            return EvidenceErrors.DatabaseRefused.With($"the SQLite file {real} is outside the repo {request.Repo} and the run directory; db-run reads files inside those only");
        }

        return new SqliteTarget(real, RunPath.Combine(request.RunDirectory, WorkFolder), seedSql);
    }

    private async ValueTask<Result<DbTarget>> PostgresAsync(DbRunRequest request, string name, CancellationToken cancellationToken)
    {
        var loaded = await targets.LoadAsync(request.Repo, request.ConfigPath, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        if (!loaded.Value.DatabaseMap.TryGetValue(name, out var entry))
        {
            var known = loaded.Value.DatabaseMap.Count == 0 ? "none" : string.Join(", ", loaded.Value.DatabaseMap.Keys.Order(StringComparer.Ordinal));
            return EvidenceErrors.DatabaseRefused.With($"no database entry '{name}' in the targets config (it names: {known}); add it to <repo>/{TargetsFile.RepoRelativePath} or pass --config");
        }

        if (!entry.Disposable)
        {
            return EvidenceErrors.DatabaseRefused.With($"entry '{name}' is not marked disposable: db-run reads only a throwaway database");
        }

        return secrets.Get(entry.PasswordEnv) is { } login
            ? new PostgresTarget(entry, login)
            : EvidenceErrors.CredentialsMissing.With($"environment variable {entry.PasswordEnv} (database {name}) is not set");
    }

    // A relative file resolves against the script's directory; a missing one is AW-EVIDENCE-010 before any lock.
    private async ValueTask<Result<string?>> ReadTextAsync(string scriptPath, string? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return (string?)null;
        }

        var path = file.StartsWith('/') ? file : RunPath.Combine(scriptPath[..scriptPath.LastIndexOf('/')], file);
        var bytes = await files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        return bytes.Map(static b => (string?)Encoding.UTF8.GetString(b));
    }
}
