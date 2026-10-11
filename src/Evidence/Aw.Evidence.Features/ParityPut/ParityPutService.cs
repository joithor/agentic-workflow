using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Publishing;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ParityPut;

/// <summary>Reads the signed upload and builds the PUT's argv (run it within a minute of signing).</summary>
internal sealed class ParityPutService(IRunFiles files, JsonSerializerOptions json) : IService<ParityPutRequest, ParityPutResponse>
{
    public async ValueTask<Result<ParityPutResponse>> Handle(ParityPutRequest request, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(request.UploadFile, cancellationToken).ConfigureAwait(false);
        var upload = bytes.Bind(b => Parse(b, request.UploadFile));
        return upload.Map(u => new ParityPutResponse(PutCommand.Build(u, request.File)));
    }

    private Result<SignedUpload> Parse(byte[] content, string path)
    {
        var parsed = AwJson.Deserialize<SignedUpload>(content, json);
        return parsed.IsError ? EvidenceErrors.Invalid.With($"{path} is not a signed upload {{url, headers}}: {parsed.Error.Message}") : parsed;
    }
}
