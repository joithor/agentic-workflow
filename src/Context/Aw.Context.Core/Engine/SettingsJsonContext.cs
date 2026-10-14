using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>The package's source-generated JSON context for the profile section it reads (used only through AwJson).</summary>
[JsonSerializable(typeof(ContextSection))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
