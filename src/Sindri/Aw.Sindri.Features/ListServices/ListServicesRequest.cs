using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListServices;

/// <summary>List the approved services with their last run and next slot (GET /sindri/services, API spec §3.1).</summary>
public sealed record ListServicesRequest() : IQuery<ListServicesResponse>;
