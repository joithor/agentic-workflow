using System.Text.Json.Serialization;
using Aw.BugFix.Features.Init;
using Aw.Kernel;

namespace Aw.BugFix.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(InitRequest))]
[JsonSerializable(typeof(InitResponse))]
internal sealed partial class BugFixJsonContext : JsonSerializerContext;
