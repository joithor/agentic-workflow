// scripts/scaffold/lib/package-templates.mjs — the files /new-package writes (docs/csharp/scaffolds/new-package.md).
// Each function returns the file text for one generated file. `p` is the resolved input:
//   { name, lower, binary, tier, errorPrefix, note, shape, host, depends: [names] }
// Paths and namespaces: full shape Aw.<N>.Core|Features|Infrastructure|Host; small shape Aw.<N>.Core and
// Aw.<N>.App, with the App's namespaces Aw.<N>.App (composition root), .App.Features.<Slice>,
// .App.Infrastructure and .App.Commands (tests/Aw.Architecture.Tests, ARCH-PLACE-001).

const ivt = (names) => names.map((n) => `    <InternalsVisibleTo Include="${n}" />`).join("\n");
const refs = (paths) => paths.map((r) => `    <ProjectReference Include="${r}" />`).join("\n");
const pkgs = (names) => names.map((n) => `    <PackageReference Include="${n}" />`).join("\n");

const csproj = ({ sdk = "Microsoft.NET.Sdk", comment, props = [], packages = [], projects = [], visibleTo = [], extra = "" }) => {
  const groups = [];
  if (packages.length) groups.push(`  <ItemGroup>\n${pkgs(packages)}\n  </ItemGroup>`);
  if (projects.length) groups.push(`  <ItemGroup>\n${refs(projects)}\n  </ItemGroup>`);
  if (visibleTo.length) groups.push(`  <ItemGroup>\n${ivt(visibleTo)}\n  </ItemGroup>`);
  if (extra) groups.push(extra);
  return `<Project Sdk="${sdk}">
  <!-- ${comment} -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
${props.map((x) => `    ${x}`).join("\n")}
  </PropertyGroup>

${groups.join("\n\n")}
</Project>
`;
};

const kernel = "../../Aw.Kernel/Aw.Kernel.csproj";
const dependsCores = (p) => p.depends.map((d) => `../../${d}/Aw.${d}.Core/Aw.${d}.Core.csproj`);

// ---------------------------------------------------------------- Core

export const coreCsproj = (p) =>
  csproj({
    comment: `Aw.${p.name}.Core (scaffolded by /new-package): domain types, typed ids, value objects, pure logic, port
       interfaces and errors. No I/O, clock or random ids (BannedSymbols.txt, spec §4b.1). ${p.note}`,
    props: [`<RootNamespace>Aw.${p.name}.Core</RootNamespace>`],
    packages: ["Microsoft.Extensions.DependencyInjection.Abstractions", "Vogen"],
    projects: [kernel],
    visibleTo: [`Aw.${p.name}.Tests`, `Aw.${p.name}.Testing`],
  });

export const vogenDefaults = () => `using Vogen;

// No Vogen conversions: the System.Text.Json source generator can't see Vogen's converters and silently writes {}.
// The rest keeps an id's public surface to what ITypedId needs (Value, the bool TryFrom, IParsable).
[assembly: VogenDefaults(
    conversions: Conversions.None,
    systemTextJsonConverterFactoryGeneration: SystemTextJsonConverterFactoryGeneration.Omit,
    primitiveEqualityGeneration: PrimitiveEqualityGeneration.Omit,
    toPrimitiveCasting: CastOperator.None,
    fromPrimitiveCasting: CastOperator.None,
    tryFromGeneration: TryFromGeneration.GenerateBoolMethod)]
`;

export const errors = (p) => `using Aw.Kernel;

namespace Aw.${p.name}.Core;

/// <summary>The ${p.name} error catalogue (codes AW-${p.errorPrefix}-NNN).</summary>
public static class ${p.name}Errors
{
    /// <summary>AW-${p.errorPrefix}-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-${p.errorPrefix}-001", ErrorKind.Invalid, "${p.name} input failed validation");

    /// <summary>Every ${p.name} error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
`;

export const coreRegistration = (p) => `using Microsoft.Extensions.DependencyInjection;

namespace Aw.${p.name}.Core;

/// <summary>Registers Aw.${p.name}.Core's types.</summary>
public static class ${p.name}CoreServiceCollectionExtensions
{
    /// <summary>Adds the ${p.name} domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection Add${p.name}Core(this IServiceCollection services) => services;
}
`;

// ---------------------------------------------------------------- Features (full) / App features (small)

export const featuresCsproj = (p) =>
  csproj({
    comment: `Aw.${p.name}.Features (scaffolded by /new-package): one folder per use case (slice): request and response
       records, the service and its validator (spec §4b.2). Another package only through its Core.`,
    props: [`<RootNamespace>Aw.${p.name}.Features</RootNamespace>`],
    packages: ["Microsoft.Extensions.DependencyInjection.Abstractions"],
    projects: ["../Aw." + p.name + ".Core/Aw." + p.name + ".Core.csproj", kernel, ...dependsCores(p)],
    visibleTo: [`Aw.${p.name}.Tests`, `Aw.${p.name}.Testing`],
  });

/** The features registration. /new-slice inserts one `_ = services...;` block per slice before `return services;`. */
export const featuresRegistration = (p, ns, visibility) => `using Microsoft.Extensions.DependencyInjection;

namespace ${ns};

/// <summary>Registers ${ns}'s slices: each service, its validator and its decorators (outermost first).</summary>
${visibility} static class ${p.name}FeaturesServiceCollectionExtensions
{
    /// <summary>Adds every ${p.name} use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    ${visibility} static IServiceCollection Add${p.name}Features(this IServiceCollection services)
    {
        return services;
    }
}
`;

// ---------------------------------------------------------------- Infrastructure

export const infrastructureCsproj = (p) =>
  csproj({
    comment: `Aw.${p.name}.Infrastructure (scaffolded by /new-package): the adapters behind Core's ports (SQL repositories,
       processes, HTTP, files) and this package's settings, read from the environment Composition passes (spec §4b.1,
       .agents/rules/csharp.md Seams). Its row and DTO types stay internal (ARCH-INFRA-001).`,
    props: [`<RootNamespace>Aw.${p.name}.Infrastructure</RootNamespace>`],
    packages: ["Microsoft.Extensions.DependencyInjection.Abstractions"],
    projects: ["../Aw." + p.name + ".Core/Aw." + p.name + ".Core.csproj", kernel],
    visibleTo: [`Aw.${p.name}.Tests`, `Aw.${p.name}.Testing`],
  });

export const boundaryEditorconfig = (what) => `root = false

# ${what}: catching every exception is allowed here (CA1031, .agents/rules/csharp.md "Types and errors").
[*.cs]
dotnet_diagnostic.CA1031.severity = none
`;

export const smallEditorconfig = () => `root = false

# The boundary folders of a small package's App: catching every exception is allowed only here (CA1031,
# .agents/rules/csharp.md "Types and errors").
[{Infrastructure/**.cs,Commands/**.cs,Program.cs,Composition.cs}]
dotnet_diagnostic.CA1031.severity = none
`;

/** A small package's Testing project: its builder root and builders are internal (App's records are). */
export const smallTestingEditorconfig = () => `root = false

# A small package's builders and builder root are internal, because App's records are; Aw.<Pkg>.Tests uses them
# through InternalsVisibleTo, which MA0182 ("internal type is apparently never used") can't see.
[*.cs]
dotnet_diagnostic.MA0182.severity = none
`;

export const settings = (p, ns) => `namespace ${ns};

/// <summary>${p.name}'s infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where ${p.name} keeps its state: $AW_STATE_DIR/${p.lower}, or ~/.agentic-workflow/${p.lower}.</param>
internal sealed record ${p.name}Settings(string StateDirectory);
`;

export const settingsFromEnvironment = (p, ns) => `namespace ${ns};

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    internal static ${p.name}Settings Read(Func<string, string?> environment)
        => new(StateDirectory: environment("AW_STATE_DIR") is { Length: > 0 } root
            ? Path.Combine(root, "${p.lower}")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow", "${p.lower}"));
}
`;

export const infrastructureRegistration = (p, ns, visibility) => `using Microsoft.Extensions.DependencyInjection;

namespace ${ns};

/// <summary>Registers ${ns}'s adapters and settings. A second technology for a port gets its own Add${p.name}&lt;Tech&gt;Infrastructure method.</summary>
${visibility} static class ${p.name}InfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the ${p.name} adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    ${visibility} static IServiceCollection Add${p.name}Infrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));
}
`;

// ---------------------------------------------------------------- Host (full) / App root (small)

export const hostCsproj = (p) => {
  const http = p.host === "http";
  return csproj({
    sdk: http ? "Microsoft.NET.Sdk.Web" : "Microsoft.NET.Sdk",
    comment: `Aw.${p.name}.Host (scaffolded by /new-package): the composition root and the ${http ? "minimal API endpoints" : "CLI commands"}
       (one per slice). NativeAOT entry point '${p.binary}' (spec §4b.6); the only project that uses Infrastructure, in
       Composition.`,
    props: ["<OutputType>Exe</OutputType>", "<PublishAot>true</PublishAot>", `<AssemblyName>${p.binary}</AssemblyName>`, `<RootNamespace>Aw.${p.name}.Host</RootNamespace>`],
    packages: http ? [] : ["Microsoft.Extensions.DependencyInjection"],
    projects: [`../Aw.${p.name}.Core/Aw.${p.name}.Core.csproj`, `../Aw.${p.name}.Features/Aw.${p.name}.Features.csproj`, `../Aw.${p.name}.Infrastructure/Aw.${p.name}.Infrastructure.csproj`, kernel],
    visibleTo: [`Aw.${p.name}.Tests`],
  });
};

export const appCsproj = (p) => {
  const exe = p.host === "cli";
  return csproj({
    comment: `Aw.${p.name}.App (scaffolded by /new-package, small shape): features, infrastructure${exe ? " and the CLI host" : ""} in one
       project, with the same dependency direction by namespace (spec §4b.1; ARCH-* in tests/Aw.Architecture.Tests).
       Nothing outside the package references it, so its types are internal (CA1515).${exe ? ` NativeAOT entry point '${p.binary}'.` : ""}`,
    props: [
      ...(exe ? ["<OutputType>Exe</OutputType>", "<PublishAot>true</PublishAot>", `<AssemblyName>${p.binary}</AssemblyName>`] : []),
      `<RootNamespace>Aw.${p.name}.App</RootNamespace>`,
    ],
    packages: exe ? ["Microsoft.Extensions.DependencyInjection"] : ["Microsoft.Extensions.DependencyInjection.Abstractions"],
    projects: [`../Aw.${p.name}.Core/Aw.${p.name}.Core.csproj`, kernel, ...dependsCores(p)],
    visibleTo: [`Aw.${p.name}.Tests`, `Aw.${p.name}.Testing`],
  });
};

/** Composition: `ns` is the root namespace (Aw.N.Host or Aw.N.App); `usings` the namespaces of the Add…() methods. */
export const composition = (p, ns, usings, { webSdk }) => {
  return `${usingsBlock([...usings, ...(webSdk ? [] : ["Microsoft.Extensions.DependencyInjection"])])}

namespace ${ns};

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .Add${p.name}Core()
            .Add${p.name}Features()
            .Add${p.name}Infrastructure(environment);
}
`;
};

/** A sorted using block (System first, then ordinal), as dotnet format expects. */
export const usingsBlock = (names) => [...new Set(names)].sort(byNamespace).map((u) => `using ${u};`).join("\n");

/** System namespaces first, then the rest, each ordinal (dotnet_sort_system_directives_first). */
export function byNamespace(a, b) {
  const sa = a === "System" || a.startsWith("System.");
  const sb = b === "System" || b.startsWith("System.");
  if (sa !== sb) return sa ? -1 : 1;
  return a < b ? -1 : a > b ? 1 : 0;
}

export const jsonContext = (p, ns) => `using System.Text.Json.Serialization;
using Aw.Kernel;

namespace ${ns};

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
internal sealed partial class ${p.name}JsonContext : JsonSerializerContext;
`;

export const programCli = (p, ns) => `${usingsBlock([ns, `${ns}.Commands`, "Aw.Kernel.Json", "Microsoft.Extensions.DependencyInjection"])}

var services = Composition.Compose(new ServiceCollection(), Environment.GetEnvironmentVariable, TimeProvider.System);
var json = AwJson.CreateOptions(${p.name}JsonContext.Default);   // the one Kernel JSON profile
var provider = services.BuildServiceProvider();   // validation runs in HostTests, not at CLI startup
await using (provider.ConfigureAwait(false))
{
    return await CommandTable.Run(args, provider, json).ConfigureAwait(false);
}
`;

/** The command table. /new-slice adds one `"<command>" => ...` arm per slice above the `_ =>` arm. */
export const commandTable = (p, ns) => `using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace ${ns}.Commands;

/// <summary>Command name → handler. Each handler binds its arguments, calls one service and maps the result to an exit code.</summary>
internal static class CommandTable
{
    internal static async Task<int> Run(string[] args, IServiceProvider provider, JsonSerializerOptions json)
    {
        if (args.Length == 0)
        {
            return CommandOutput.Usage("${p.binary} <command> [arguments]");
        }

        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var rest = args[1..];
            return args[0] switch
            {
                _ => CommandOutput.Usage($"unknown command '{args[0]}'"),
            };
        }
    }
}
`;

/** Writes a command's result: the response as one JSON line on stdout (exit 0), or the error on stderr (exit 1). */
export const commandOutput = (p, ns) => `using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace ${ns}.Commands;

/// <summary>Maps a command's Result to output and an exit code: 0 success, 1 an expected error, 2 a usage error.</summary>
internal static class CommandOutput
{
    internal static int Write<T>(Result<T> result, JsonSerializerOptions json)
    {
        if (result.IsError)
        {
            Console.Error.WriteLine(Encoding.UTF8.GetString(AwJson.Serialize(result.Error, json)));
            return 1;
        }

        Console.Out.WriteLine(Encoding.UTF8.GetString(AwJson.Serialize(result.Value, json)));
        return 0;
    }

    internal static int Usage(string message)
    {
        Console.Error.WriteLine($"usage: {message}");
        return 2;
    }
}
`;

// ---------------------------------------------------------------- Host (http)

export const programHttp = (p) => `using Aw.${p.name}.Host;

var app = LoopbackBinding.Build(args, Environment.GetEnvironmentVariable, TimeProvider.System);
await using (app.ConfigureAwait(false))
{
    _ = Pipeline.Configure(app);
    await app.RunAsync().ConfigureAwait(false);
}
`;

/** The request pipeline, shared by Program.cs and the tests (like Composition for DI), so the tests run the real one. */
export const pipeline = (p) => `using Aw.${p.name}.Host.Endpoints;

namespace Aw.${p.name}.Host;

/// <summary>The request pipeline Program.cs runs: the loopback guard, Vogen's 400 mapping, then the endpoint table.</summary>
internal static class Pipeline
{
    internal static WebApplication Configure(WebApplication app)
        => app.UseLoopbackGuard().UseValueObjectValidation().Map${p.name}Endpoints();
}
`;

export const loopbackBinding = (p) => `using System.Globalization;
using System.Net;
using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.${p.name}.Host;

/// <summary>
/// Builds the web host so that the only listener is 127.0.0.1 (spec §4d runtime hardening): no command-line
/// arguments and no environment variables reach the web host's configuration, PreferHostingUrls is off, and Kestrel
/// listens with Listen(IPAddress.Loopback, port) only. The port comes from AW_${p.name.toUpperCase()}_PORT (0, the default, picks a free one).
/// </summary>
internal static class LoopbackBinding
{
    internal const string PortVariable = "AW_${p.name.toUpperCase()}_PORT";

    internal static WebApplication Build(string[] args, Func<string, string?> environment, TimeProvider clock)
    {
        if (args.Length > 0)
        {
            throw new ArgumentException("This host takes no command-line arguments: its address is fixed to loopback.", nameof(args));
        }

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
        builder.Configuration.Sources.Clear();   // drops the ASPNETCORE_/DOTNET_ environment and command-line sources
        _ = builder.Configuration.AddInMemoryCollection();   // the host still writes its own settings somewhere
        _ = builder.WebHost.PreferHostingUrls(preferHostingUrls: false);
        _ = builder.WebHost.UseKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, PortFrom(environment)));
        _ = Composition.Compose(builder.Services, environment, clock);
        _ = builder.Services.ConfigureHttpJsonOptions(o => CopyProfile(o.SerializerOptions));
        _ = builder.Services.AddProblemDetails();   // its post-configure step adds ProblemDetails' JSON context to the chain
        return builder.Build();
    }

    private static int PortFrom(Func<string, string?> environment)
        => int.TryParse(environment(PortVariable), NumberStyles.None, CultureInfo.InvariantCulture, out var port) ? port : 0;

    /// <summary>Copies the Kernel JSON profile into ASP.NET's options (AwJson's options are read-only and can't be assigned).</summary>
    private static void CopyProfile(JsonSerializerOptions target)
    {
        var profile = AwJson.CreateOptions(${p.name}JsonContext.Default);
        target.TypeInfoResolverChain.Clear();   // AddProblemDetails' post-configure step adds ProblemDetails' context after this runs
        target.TypeInfoResolverChain.Add(${p.name}JsonContext.Default);
        target.PropertyNamingPolicy = profile.PropertyNamingPolicy;
        target.PropertyNameCaseInsensitive = profile.PropertyNameCaseInsensitive;   // the Web defaults are case-insensitive
        target.NumberHandling = profile.NumberHandling;                             // and read numbers from strings
        target.DefaultIgnoreCondition = profile.DefaultIgnoreCondition;
        target.UnmappedMemberHandling = profile.UnmappedMemberHandling;
        target.Encoder = profile.Encoder;
        target.RespectNullableAnnotations = profile.RespectNullableAnnotations;
        target.RespectRequiredConstructorParameters = profile.RespectRequiredConstructorParameters;
        foreach (var converter in profile.Converters)
        {
            target.Converters.Add(converter);
        }
    }
}
`;

export const loopbackGuard = (p) => `namespace Aw.${p.name}.Host;

/// <summary>Refuses a non-loopback Host header (400, DNS rebinding) and a non-loopback browser Origin (403), spec §4d.</summary>
internal static class LoopbackGuard
{
    internal static WebApplication UseLoopbackGuard(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            if (!IsLoopback(context.Request.Host.Host))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0 && !(Uri.TryCreate(origin, UriKind.Absolute, out var uri) && IsLoopback(uri.Host)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context).ConfigureAwait(false);
        });
        return app;
    }

    private static bool IsLoopback(string host)
        => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
            || string.Equals(host, "[::1]", StringComparison.Ordinal)
            || string.Equals(host, "::1", StringComparison.Ordinal);
}
`;

export const valueObjectValidation = (p) => `using Vogen;

namespace Aw.${p.name}.Host;

/// <summary>Maps Vogen's validation exception (an uninitialized id from an empty route or query value) to 400.</summary>
internal static class ValueObjectValidation
{
    internal static WebApplication UseValueObjectValidation(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (ValueObjectValidationException) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        });
        return app;
    }
}
`;

export const httpResults = (p) => `using Aw.Kernel;

namespace Aw.${p.name}.Host;

/// <summary>Maps a returned Result to TypedResults (a value) or ProblemDetails (an error, by its kind).</summary>
internal static class HttpResults
{
    internal static IResult ToHttpResult<T>(this Result<T> result)
        => result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.Problem(title: result.Error.Code, detail: result.Error.Message, statusCode: StatusOf(result.Error.Kind));

    private static int StatusOf(ErrorKind kind) => kind switch
    {
        ErrorKind.Invalid => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.RateLimited or ErrorKind.BudgetExceeded => StatusCodes.Status429TooManyRequests,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorKind.Timeout => StatusCodes.Status504GatewayTimeout,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}
`;

/** The endpoint table. /new-slice adds one `.Map<Slice>()` call per slice. */
export const endpointTable = (p) => `namespace Aw.${p.name}.Host.Endpoints;

/// <summary>Maps every ${p.name} endpoint under /${p.lower}; one Map&lt;Slice&gt;() call per slice.</summary>
internal static class EndpointTable
{
    internal static WebApplication Map${p.name}Endpoints(this WebApplication app)
    {
        _ = app.MapGroup("/${p.lower}");
        return app;
    }
}
`;

// ---------------------------------------------------------------- tests

export const testsCsproj = (p, srcProjects) =>
  csproj({
    comment: `Aw.${p.name}.Tests (scaffolded by /new-package): by feature, one Lane trait per test (spec §4b.5).`,
    props: ["<OutputType>Exe</OutputType>", `<RootNamespace>Aw.${p.name}.Tests</RootNamespace>`, ...(p.host === "http" ? ["<EnableRequestDelegateGenerator>true</EnableRequestDelegateGenerator>"] : [])],
    packages: ["xunit.v3", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.TimeProvider.Testing", ...(p.host === "http" ? ["Microsoft.AspNetCore.Mvc.Testing"] : [])],
    projects: [...srcProjects.map((s) => `../../${s}`), `../Aw.${p.name}.Testing/Aw.${p.name}.Testing.csproj`],
    extra: `  <ItemGroup>\n    <Using Include="Xunit" />\n  </ItemGroup>`,
  });

export const testingCsproj = (p) =>
  csproj({
    comment: `Aw.${p.name}.Testing (scaffolded by /new-package): builders, in-memory fakes and contract bases for ${p.name}'s
       ports (spec §4b.5). Test support: only *.Tests and *.Testing projects reference it.`,
    props: [`<RootNamespace>Aw.${p.name}.Testing</RootNamespace>`],
    projects: [
      `../../src/${p.name}/Aw.${p.name}.Core/Aw.${p.name}.Core.csproj`,
      p.shape === "small" ? `../../src/${p.name}/Aw.${p.name}.App/Aw.${p.name}.App.csproj` : `../../src/${p.name}/Aw.${p.name}.Features/Aw.${p.name}.Features.csproj`,
      "../Aw.Kernel.Testing/Aw.Kernel.Testing.csproj",
    ],
    visibleTo: p.shape === "small" ? [`Aw.${p.name}.Tests`] : [],
  });

export const builderRoot = (p) => `namespace Aw.${p.name}.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
${p.shape === "small" ? "internal" : "public"} static class A
{
}
`;

export const errorsTests = (p) => `${usingsBlock([`Aw.${p.name}.Core`, "Aw.Kernel"])}

namespace Aw.${p.name}.Tests;

public sealed class ${p.name}ErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(${p.name}Errors.All).IsSuccess);
}
`;

export const hostTests = (p, ns) => `${usingsBlock([ns, "Aw.Kernel.Services", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.Time.Testing"])}

namespace Aw.${p.name}.Tests;

public sealed class HostTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_registered_service_resolves_in_a_scope()
    {
        var stateRoot = Path.Combine(Path.GetTempPath(), "aw-${p.lower}-di-test");
        var services = Composition.Compose(
            new ServiceCollection(), name => name == "AW_STATE_DIR" ? stateRoot : null, new FakeTimeProvider());
        var serviceTypes = services
            .Select(d => d.ServiceType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IService<,>))
            .ToList();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotEmpty(serviceTypes);
        Assert.All(serviceTypes, t => Assert.NotNull(scope.ServiceProvider.GetRequiredService(t)));
    }
}
`;

export const loopbackCollection = (p) => `namespace Aw.${p.name}.Tests;

/// <summary>LoopbackBindingTests set process environment variables, so they never run beside another test.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironment
{
    public const string Name = "process environment";
}
`;

export const loopbackTests = (p) => `${usingsBlock(["System.Net", `Aw.${p.name}.Host`, "Aw.Kernel", "Microsoft.AspNetCore.Builder", "Microsoft.AspNetCore.Hosting.Server", "Microsoft.AspNetCore.Hosting.Server.Features", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.Time.Testing"])}

namespace Aw.${p.name}.Tests;

[Collection(ProcessEnvironment.Name)]
public sealed class LoopbackBindingTests
{
    /// <summary>Spec §4d's override vectors, as "NAME=value" pairs separated by ';' (PREFERHOSTINGURLS needs a URL to prefer).</summary>
    public static TheoryData<string> Vectors =>
    [
        "ASPNETCORE_URLS=http://0.0.0.0:0",
        "DOTNET_URLS=http://0.0.0.0:0",
        "ASPNETCORE_HTTP_PORTS=0",
        "ASPNETCORE_PREFERHOSTINGURLS=true;ASPNETCORE_URLS=http://0.0.0.0:0",
        "Kestrel__Endpoints__Any__Url=http://0.0.0.0:0",
        "ASPNETCORE_Kestrel__Endpoints__Any__Url=http://0.0.0.0:0",
    ];

    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(Vectors))]
    public async Task The_only_listener_is_loopback_whatever_the_environment_says(string vector)
    {
        var pairs = vector.Split(';').Select(p => p.Split('=', 2)).ToList();
        var before = pairs.ToDictionary(p => p[0], p => Environment.GetEnvironmentVariable(p[0]), StringComparer.Ordinal);
        pairs.ForEach(p => Environment.SetEnvironmentVariable(p[0], p[1]));
        try
        {
            var addresses = await ListenerAddresses();

            var only = Assert.Single(addresses);
            Assert.StartsWith("http://127.0.0.1:", only, StringComparison.Ordinal);
        }
        finally
        {
            foreach (var (name, value) in before)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("--urls", "http://0.0.0.0:0")]
    [InlineData("--Kestrel:Endpoints:Any:Url", "http://0.0.0.0:0")]
    public void Command_line_arguments_are_refused(string option, string value)
    {
        var refused = Assert.Throws<ArgumentException>(
            () => LoopbackBinding.Build([option, value], _ => null, new FakeTimeProvider()));

        Assert.Equal("args", refused.ParamName);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_non_loopback_host_header_is_refused_with_400()
    {
        await using var app = await Started();
        using var client = new HttpClient { BaseAddress = new Uri(Address(app)) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "example.com";

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_error_result_is_problem_details_with_its_code()
    {
        var app = LoopbackBinding.Build([], _ => null, new FakeTimeProvider());
        await using (app)
        {
            _ = Pipeline.Configure(app);
            _ = app.MapGet("/probe-error", static () => ((Result<Unit>)KernelErrors.Invalid.With("probe")).ToHttpResult());
            await app.StartAsync(TestContext.Current.CancellationToken);
            using var client = new HttpClient { BaseAddress = new Uri(Address(app)) };

            using var response = await client.GetAsync(new Uri("/probe-error", UriKind.Relative), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains(KernelErrors.Invalid.Code, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_non_loopback_browser_origin_is_refused_with_403()
    {
        await using var app = await Started();
        using var client = new HttpClient { BaseAddress = new Uri(Address(app)) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", "https://example.com");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<WebApplication> Started()
    {
        var app = LoopbackBinding.Build([], _ => null, new FakeTimeProvider());
        _ = Pipeline.Configure(app);   // the pipeline Program.cs runs
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static string Address(WebApplication app) => Addresses(app).Single();

    private static IReadOnlyList<string> Addresses(WebApplication app)
        => [.. app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses ?? []];

    private static async Task<IReadOnlyList<string>> ListenerAddresses()
    {
        await using var app = await Started();
        return Addresses(app);
    }
}
`;

/** The http Host's JSON options are the AwJson profile, setting by setting (ASP.NET starts from the looser Web defaults). */
export const httpJsonProfileTests = (p) => `${usingsBlock(["System.Text.Json.Serialization.Metadata", `Aw.${p.name}.Host`, "Aw.Kernel.Json", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.Options", "Microsoft.Extensions.Time.Testing"])}
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Aw.${p.name}.Tests;

public sealed class HttpJsonProfileTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task The_http_json_options_are_the_Kernel_profile()
    {
        await using var app = LoopbackBinding.Build([], _ => null, new FakeTimeProvider());
        var http = app.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        var profile = AwJson.CreateOptions(${p.name}JsonContext.Default);

        // Exactly ASP.NET's ProblemDetails context, then ours: no reflection resolver, so a missing [JsonSerializable]
        // fails here as it would in the NativeAOT host.
        Assert.Equal(2, http.TypeInfoResolverChain.Count);
        Assert.Same(${p.name}JsonContext.Default, http.TypeInfoResolverChain[1]);
        Assert.DoesNotContain(http.TypeInfoResolverChain, r => r is DefaultJsonTypeInfoResolver);
        Assert.NotNull(http.TypeInfoResolver?.GetTypeInfo(typeof(ProblemDetails), http));   // error results serialize
        Assert.Equal(profile.PropertyNamingPolicy, http.PropertyNamingPolicy);
        Assert.Equal(profile.PropertyNameCaseInsensitive, http.PropertyNameCaseInsensitive);
        Assert.Equal(profile.NumberHandling, http.NumberHandling);
        Assert.Equal(profile.DefaultIgnoreCondition, http.DefaultIgnoreCondition);
        Assert.Equal(profile.UnmappedMemberHandling, http.UnmappedMemberHandling);
        Assert.Equal(profile.Encoder, http.Encoder);
        Assert.Equal(profile.RespectNullableAnnotations, http.RespectNullableAnnotations);
        Assert.Equal(profile.RespectRequiredConstructorParameters, http.RespectRequiredConstructorParameters);
        Assert.Equal(profile.Converters.Select(c => c.GetType()), http.Converters.Select(c => c.GetType()));
    }
}
`;
