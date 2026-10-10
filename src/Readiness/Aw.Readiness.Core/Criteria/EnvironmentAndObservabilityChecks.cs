namespace Aw.Readiness.Core.Criteria;

/// <summary>Development Environment and Debugging and Observability criteria for the C# template.</summary>
internal static class EnvironmentAndObservabilityChecks
{
    private const string Telemetry = "src/Aw.Kernel.Telemetry";

    internal static IEnumerable<CatalogueEntry> Entries()
    {
        yield return CatalogueEntry.Of(new("DE-1", Pillar.DevelopmentEnvironment, 2), "Devcontainer",
            "add .devcontainer/devcontainer.json for humans (spec §4d: never the agent sandbox)", c => Present(c, ".devcontainer/devcontainer.json", ".devcontainer.json"));
        yield return CatalogueEntry.Of(new("DE-2", Pillar.DevelopmentEnvironment, 2), "Environment template",
            "add .env.example covering every variable the packages read", c => Present(c, ".env.example"));
        yield return CatalogueEntry.Of(new("DE-3", Pillar.DevelopmentEnvironment, 2), "Local services setup",
            "keep setup.sh (one-command setup)", c => Present(c, "setup.sh"));
        yield return CatalogueEntry.Of(new("OBS-1", Pillar.DebuggingAndObservability, 3), "Structured logging (one shared JSON-lines logger)",
            "build Aw.Kernel.Telemetry's JSON-lines logger provider (spec §4b.1, W7)", c => InTelemetry(c, "ILoggerProvider", "a logger provider"));
        yield return CatalogueEntry.Of(new("OBS-2", Pillar.DebuggingAndObservability, 3), "Distributed tracing (OpenTelemetry spans)",
            "register the ActivitySources in Aw.Kernel.Telemetry's tracer provider (AddSource)", c => InTelemetry(c, ".AddSource(", "a tracer provider"));
        yield return CatalogueEntry.Of(new("OBS-3", Pillar.DebuggingAndObservability, 3), "Metrics collection (OpenTelemetry meters)",
            "register the Meters in Aw.Kernel.Telemetry's meter provider (AddMeter)", c => InTelemetry(c, ".AddMeter(", "a meter provider"));
    }

    private static Verdict Present(RepoContext c, params string[] paths)
    {
        var found = c.FirstExisting(paths);
        return Verdict.When(found is not null, $"{found} present", $"no {string.Join(" or ", paths)}");
    }

    private static Verdict InTelemetry(RepoContext c, string marker, string what)
    {
        if (!c.Has($"{Telemetry}/Aw.Kernel.Telemetry.csproj"))
        {
            return Verdict.Fail($"no {Telemetry} project");
        }

        var files = c.FilesContaining(Telemetry, ".cs", marker);
        return files.Count > 0
            ? Verdict.Pass($"Aw.Kernel.Telemetry has {what}: {files[0]}")
            : Verdict.Fail($"Aw.Kernel.Telemetry has no {what} ({marker})");
    }
}
