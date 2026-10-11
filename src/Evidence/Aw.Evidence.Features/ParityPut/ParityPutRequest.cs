namespace Aw.Evidence.Features.ParityPut;

/// <summary>Builds the argv for the signed PUT that uploads one image.</summary>
/// <param name="UploadFile">The signed upload as JSON {url, headers} (absolute).</param>
/// <param name="File">The image to upload (absolute).</param>
public sealed record ParityPutRequest(string UploadFile, string File);
