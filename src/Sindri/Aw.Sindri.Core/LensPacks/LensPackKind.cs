namespace Aw.Sindri.Core.LensPacks;

/// <summary>Which loop a lens pack serves (line spec §5.2 'applies'): the design station's lens rounds or the review loop's.</summary>
public enum LensPackKind
{
    /// <summary>The design lens loop (severities CRITICAL, HIGH, MEDIUM, LOW).</summary>
    Design,

    /// <summary>The review loop (severities blocking, issue, suggestion, nit).</summary>
    Review,
}
