using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// Doctor's 'context:onyx' check (context spec §8.8): the onyx row of the engine's sources view for the approved profile,
/// asked as an in-process service, so it shows the source's own probe: the loopback url and pinned version
/// (SND-CONTEXT-055), whether the key can search (050) and the key's role and account type (053 warns on a person's
/// admin token: the dedicated user is told by its email, a service account by its type). A profile that names no onyx source, or no approved profile, is 'ok: not configured'. Before probing,
/// it resolves 'context.sources.onyx.token' through <paramref name="secrets"/>: a pointer that doesn't resolve fails
/// with its SND-SECRET code, which names the scheme and the target (a variable, a file and its mode), never a value.
/// The engine sees only "no usable key" (its 002), so this is where the reason is shown.
/// </summary>
/// <param name="engine">The context engine.</param>
/// <param name="secrets">Resolves the key's pointer, as Sindri Host does for the engine.</param>
internal sealed class OnyxDoctor(IContextEngine engine, ISecretPointers secrets)
{
    /// <summary>The check's name.</summary>
    internal const string Name = "context:onyx";

    private const string Setup = "docs/sindri/onyx.md";
    private static readonly SourceId _onyx = SourceId.From("onyx");

    /// <summary>Judges the onyx source the approved profile names.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>The check.</returns>
    internal async ValueTask<DoctorCheck> Check(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        if (approved is null)
        {
            return new DoctorCheck(Name, CheckState.Ok, "not configured (no profile is approved)", Fix: null);
        }

        ProfileRepoEntry[] entries = [.. (approved.Profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(approved.Profile.Context, entries);
        if (settings.IsError)
        {
            return new DoctorCheck(Name, CheckState.Fail, $"{settings.Error.Code}: {settings.Error.Message}", "sindri profile validate");
        }

        if (settings.Value.Onyx is null)
        {
            return new DoctorCheck(Name, CheckState.Ok, "not configured (the approved profile names no context.sources.onyx)", Fix: null);
        }

        var repos = ScopeRules.Usable(entries);
        if (repos.Count == 0 || (settings.Value.Repos is { } named && !repos.Any(r => named.Contains(r.Name))))
        {
            return new DoctorCheck(Name, CheckState.Warn, "the approved profile maps no repo, so no context source runs, onyx included", "map a repo in the profile's repos and context.repos");
        }

        var key = SecretPointer.Parse(settings.Value.Onyx.Token).Bind(secrets.Resolve);
        if (key.IsError)
        {
            return new DoctorCheck(Name, CheckState.Fail, $"{key.Error.Code}: {key.Error.Message}",
                $"fix context.sources.onyx.token's target as the message says, or point it at the Onyx key ({Setup})");
        }

        var scope = new ContextScope(approved.Profile.Context, repos, new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));
        var view = await engine.Sources(scope, cancellationToken).ConfigureAwait(false);
        return view.IsError
            ? new DoctorCheck(Name, CheckState.Fail, $"{view.Error.Code}: {view.Error.Message}", "sindri context sources status")
            : Judge(view.Value.Sources.FirstOrDefault(r => r.Source == _onyx));
    }

    private static DoctorCheck Judge(SourceStatusRow? row) => row switch
    {
        null => new DoctorCheck(Name, CheckState.Fail, "the engine listed no onyx source", "sindri context sources status"),
        { State: SourceState.Ok } => new DoctorCheck(Name, CheckState.Ok, row.Detail, Fix: null),
        { State: SourceState.Degraded } => new DoctorCheck(Name, CheckState.Warn, Detail(row), Fix(row.Code)),
        _ => new DoctorCheck(Name, CheckState.Fail, Detail(row), Fix(row.Code)),
    };

    private static string Detail(SourceStatusRow row) => row.Code is { } code ? $"{code}: {row.Detail}" : row.Detail;

    private static string Fix(string? code) => code switch
    {
        _ when code == ContextErrors.OnyxNotPinnedOrLoopback.Code => $"run the pinned Onyx release on 127.0.0.1, or pin the running one and approve the profile ({Setup})",
        _ when code == ContextErrors.OnyxKeyRefused.Code || code == ContextErrors.OnyxKeyNotServiceAccount.Code
            => $"use a personal access token of a dedicated admin user (`sindri onyx up` makes sindri@example.com) or a service-account key for Sindri alone, and store it where context.sources.onyx.token points ({Setup})",
        _ when code == ContextErrors.SourceNotConfigured.Code => $"store the Onyx key where context.sources.onyx.token points ({Setup})",
        _ => $"start Onyx and check context.sources.onyx.url ({Setup})",
    };
}
