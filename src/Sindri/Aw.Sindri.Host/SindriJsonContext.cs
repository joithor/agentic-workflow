using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(GetStatusRequest))]
[JsonSerializable(typeof(GetStatusResponse))]
internal sealed partial class SindriJsonContext : JsonSerializerContext;
