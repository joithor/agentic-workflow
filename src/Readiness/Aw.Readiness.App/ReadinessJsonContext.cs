using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Readiness.App.Features.CheckRepo;

namespace Aw.Readiness.App;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(CheckRepoRequest))]
[JsonSerializable(typeof(CheckRepoResponse))]
internal sealed partial class ReadinessJsonContext : JsonSerializerContext;
