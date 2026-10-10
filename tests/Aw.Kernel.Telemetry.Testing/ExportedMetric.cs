namespace Aw.Kernel.Telemetry.Testing;

/// <summary>A copy of one exported metric stream: its name and each point's tags (the SDK reuses its own objects).</summary>
/// <param name="Name">The instrument name.</param>
/// <param name="PointTags">Each point's tags, as key and value text.</param>
public sealed record ExportedMetric(string Name, IReadOnlyList<IReadOnlyDictionary<string, string?>> PointTags);
