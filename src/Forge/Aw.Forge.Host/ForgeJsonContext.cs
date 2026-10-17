using System.Text.Json.Serialization;
using Aw.Forge.Features.ParseForgeLink;
using Aw.Kernel;

namespace Aw.Forge.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(ParseForgeLinkRequest))]
[JsonSerializable(typeof(ParseForgeLinkResponse))]
internal sealed partial class ForgeJsonContext : JsonSerializerContext;
