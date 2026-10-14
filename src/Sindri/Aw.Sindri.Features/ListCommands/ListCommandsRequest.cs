using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListCommands;

/// <summary>List the command catalog (GET /sindri/commands, API spec §3.1): every action id with its class, callers, route, CLI and UI status.</summary>
public sealed record ListCommandsRequest() : IQuery<ListCommandsResponse>;
