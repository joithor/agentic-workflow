using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Publishing;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ParityStale;

/// <summary>Reads the issue's attachments and selects the stale pixel-diff ones.</summary>
internal sealed class ParityStaleService(IRunFiles files, JsonSerializerOptions json) : IService<ParityStaleRequest, ParityStaleResponse>
{
    public async ValueTask<Result<ParityStaleResponse>> Handle(ParityStaleRequest request, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(request.AttachmentsFile, cancellationToken).ConfigureAwait(false);
        var existing = bytes.Bind(b => Parse(b, request.AttachmentsFile));
        return existing.Map(e => new ParityStaleResponse(StaleAttachments.Select(e, request.Keep)));
    }

    private Result<IReadOnlyList<ExistingAttachment>> Parse(byte[] content, string path)
    {
        var parsed = AwJson.Deserialize<IReadOnlyList<ExistingAttachment>>(content, json);
        return parsed.IsError ? EvidenceErrors.Invalid.With($"{path} is not a JSON array of {{id, title}}: {parsed.Error.Message}") : parsed;
    }
}
