using Aw.Evidence.Features.RunScript;

namespace Aw.Evidence.Testing;

/// <summary>Builds a <see cref="RunScriptRequest"/>: /work/script.json into /runs/run-1, local host, no baseline, a zero lock timeout.</summary>
public sealed class RunScriptRequestBuilder
{
    /// <summary>The default script path.</summary>
    public const string ScriptPath = "/work/script.json";

    /// <summary>The default run directory.</summary>
    public const string RunDirectory = "/run/run-1";

    private RunScriptRequest _request = new(ScriptPath, RunDirectory, TimeSpan.Zero);

    /// <summary>Compares the last passed screenshot with <paramref name="baseline"/>.</summary>
    /// <param name="baseline">The baseline path.</param>
    /// <returns>This builder.</returns>
    public RunScriptRequestBuilder WithBaseline(string baseline)
    {
        _request = _request with { Baseline = baseline };
        return this;
    }

    /// <summary>Records the app build under test.</summary>
    /// <param name="appBuild">The build.</param>
    /// <returns>This builder.</returns>
    public RunScriptRequestBuilder WithAppBuild(string appBuild)
    {
        _request = _request with { AppBuild = appBuild };
        return this;
    }

    /// <summary>Keeps the verdict cache in <paramref name="cacheManifest"/>.</summary>
    /// <param name="cacheManifest">The cache file.</param>
    /// <returns>This builder.</returns>
    public RunScriptRequestBuilder WithCache(string cacheManifest)
    {
        _request = _request with { CacheManifest = cacheManifest };
        return this;
    }

    /// <summary>Writes into <paramref name="runDirectory"/>.</summary>
    /// <param name="runDirectory">The run directory.</param>
    /// <returns>This builder.</returns>
    public RunScriptRequestBuilder In(string runDirectory)
    {
        _request = _request with { RunDirectory = runDirectory };
        return this;
    }

    /// <summary>Drives <paramref name="host"/> instead of the local default.</summary>
    /// <param name="host">The --host value.</param>
    /// <param name="allowPreviewHost">--allow-preview-host.</param>
    /// <param name="allowWrites">--allow-writes.</param>
    /// <returns>This builder.</returns>
    public RunScriptRequestBuilder OnHost(string host, bool allowPreviewHost = true, bool allowWrites = false)
    {
        _request = _request with { Host = host, AllowPreviewHost = allowPreviewHost, AllowWrites = allowWrites };
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public RunScriptRequest Build() => _request;
}
