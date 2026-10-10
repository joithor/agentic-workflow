using System.Text.Json.Serialization;

namespace Aw.Readiness.Core;

/// <summary>The nine agent-readiness pillars (spec §2), in report order.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Pillar>))]
public enum Pillar
{
    /// <summary>Linters, type checking, formatting, pre-commit.</summary>
    [JsonStringEnumMemberName("styleAndValidation")]
    StyleAndValidation = 1,

    /// <summary>Build commands, pinned dependencies and runtime, releases.</summary>
    [JsonStringEnumMemberName("buildSystem")]
    BuildSystem = 2,

    /// <summary>Unit and integration tests, one test command, CI feedback, flaky tests.</summary>
    [JsonStringEnumMemberName("testing")]
    Testing = 3,

    /// <summary>README, AGENTS.md, doc freshness.</summary>
    [JsonStringEnumMemberName("documentation")]
    Documentation = 4,

    /// <summary>Devcontainer, environment template, local setup.</summary>
    [JsonStringEnumMemberName("developmentEnvironment")]
    DevelopmentEnvironment = 5,

    /// <summary>Structured logging, tracing, metrics.</summary>
    [JsonStringEnumMemberName("debuggingAndObservability")]
    DebuggingAndObservability = 6,

    /// <summary>Branch protection, secret scanning, CODEOWNERS, dependency updates.</summary>
    [JsonStringEnumMemberName("security")]
    Security = 7,

    /// <summary>Issue and PR templates, labels, decomposed work.</summary>
    [JsonStringEnumMemberName("taskDiscovery")]
    TaskDiscovery = 8,

    /// <summary>Product analytics, experiments, self-improvement.</summary>
    [JsonStringEnumMemberName("productAndExperimentation")]
    ProductAndExperimentation = 9,
}
