using System.Text.Json;

namespace Aw.Meta.Host.Commands;

/// <summary>What a command handler is given.</summary>
/// <param name="Line">The parsed arguments.</param>
/// <param name="Services">The scope's services.</param>
/// <param name="Json">The AwJson options of the package's context.</param>
/// <param name="Io">The streams.</param>
/// <param name="Cancellation">Fires on SIGINT or SIGTERM, so a long command can stop what it started.</param>
internal sealed record CommandContext(CommandLine Line, IServiceProvider Services, JsonSerializerOptions Json, ConsoleIo Io, CancellationToken Cancellation = default);
