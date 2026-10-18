namespace Aw.Sindri.Features.GetSessionPreview;

/// <summary>The answer to a preview read (collector spec CA-9): a <see cref="SessionPreview"/>, or, still HTTP 200, a <see cref="SessionPreviewUnavailable"/>.</summary>
public abstract record GetSessionPreviewResponse
{
    private protected GetSessionPreviewResponse()
    {
    }
}
