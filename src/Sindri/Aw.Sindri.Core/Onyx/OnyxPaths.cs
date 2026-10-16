namespace Aw.Sindri.Core.Onyx;

/// <summary>Where Sindri keeps Onyx's files under its home: the setup state, the fetched deployment files, and the .env and compose override beside the compose file.</summary>
/// <param name="Root">The onyx directory under the Sindri home.</param>
/// <param name="State">state.json: the steps done and the stamp of the inputs each ran with.</param>
/// <param name="DeployDir">The fetched deployment/ tree (each pinned file under its lock path).</param>
/// <param name="Compose">The pinned compose file.</param>
/// <param name="Env">The .env Sindri renders, beside the compose file.</param>
/// <param name="Override">The compose override Sindri renders, beside the compose file.</param>
public sealed record OnyxPaths(string Root, string State, string DeployDir, string Compose, string Env, string Override)
{
    /// <summary>Lays the files out under a Sindri home. Joined with '/', which every platform's file APIs accept; Core has no System.IO.</summary>
    /// <param name="sindriHome">The Sindri home (AW_SINDRI_HOME).</param>
    /// <returns>The paths.</returns>
    public static OnyxPaths For(string sindriHome)
    {
        ArgumentNullException.ThrowIfNull(sindriHome);
        var root = sindriHome.TrimEnd('/', '\\') + "/onyx";
        var deploy = root + "/deploy/deployment";
        var composeDir = deploy + "/docker_compose";
        return new OnyxPaths(root, root + "/state.json", deploy, composeDir + "/docker-compose.yml", composeDir + "/.env", composeDir + "/docker-compose.override.yml");
    }
}
