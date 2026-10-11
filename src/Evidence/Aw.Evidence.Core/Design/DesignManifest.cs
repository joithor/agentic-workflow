using System.Text.Json.Serialization;
using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Design;

/// <summary>A design-parity manifest: the route, how to know it has loaded, capture settings and the frames. Unknown keys are refused.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesignManifest
{
    /// <summary>The default viewport, 1512x982.</summary>
    public static readonly ViewportSize DefaultViewport = new(1512, 982);

    /// <summary>Creates a manifest; JSON reads through this constructor so its defaults apply to missing keys.</summary>
    /// <param name="route">The route under test.</param>
    /// <param name="ready">Visible once the route has loaded.</param>
    /// <param name="frames">The frames.</param>
    /// <param name="viewport">The viewport; default 1512x982.</param>
    /// <param name="timeoutMs">Wait limit for ready, waitFor and anchors; default 60000.</param>
    /// <param name="settleMs">Settle time around capture preparation; default 600.</param>
    /// <param name="threshold">Per-pixel colour tolerance; default 0.1.</param>
    /// <param name="includeAA">Count anti-aliased pixels; default false.</param>
    /// <param name="neutralClick">Where to click to drop focus; default 22 px in from the bottom-right corner.</param>
    /// <param name="skipNeutralClick">Skip the neutral click.</param>
    /// <param name="designNode">The design file or node.</param>
    /// <param name="login">How to sign in first.</param>
    [JsonConstructor]
    public DesignManifest(
        string route,
        ElementSelector ready,
        IReadOnlyList<DesignFrame> frames,
        ViewportSize? viewport = null,
        int timeoutMs = 60_000,
        int settleMs = 600,
        double threshold = 0.1,
        bool includeAA = false,
        Offset? neutralClick = null,
        bool skipNeutralClick = false,
        string? designNode = null,
        LoginSpec? login = null)
    {
        Route = route;
        Ready = ready;
        Frames = frames;
        Viewport = viewport ?? DefaultViewport;
        TimeoutMs = timeoutMs;
        SettleMs = settleMs;
        Threshold = threshold;
        IncludeAA = includeAA;
        NeutralClick = neutralClick;
        SkipNeutralClick = skipNeutralClick;
        DesignNode = designNode;
        Login = login;
    }

    /// <summary>The route under test; starts with '/'.</summary>
    public string Route { get; init; }

    /// <summary>Visible once the route has loaded (past any spinner).</summary>
    public ElementSelector Ready { get; init; }

    /// <summary>The frames, at least one, with unique names.</summary>
    public IReadOnlyList<DesignFrame> Frames { get; init; }

    /// <summary>The browser viewport.</summary>
    public ViewportSize Viewport { get; init; }

    /// <summary>How long to wait for ready, a frame's waitFor and its anchor, in milliseconds.</summary>
    public int TimeoutMs { get; init; }

    /// <summary>Settle time before and after capture preparation, in milliseconds.</summary>
    public int SettleMs { get; init; }

    /// <summary>Per-pixel colour tolerance, 0..1.</summary>
    public double Threshold { get; init; }

    /// <summary>Count anti-aliased pixels as differences.</summary>
    public bool IncludeAA { get; init; }

    /// <summary>Where to click to drop focus; null is 22 px in from the viewport's bottom-right corner.</summary>
    public Offset? NeutralClick { get; init; }

    /// <summary>Skip the neutral click.</summary>
    public bool SkipNeutralClick { get; init; }

    /// <summary>The design file or node all frames come from.</summary>
    public string? DesignNode { get; init; }

    /// <summary>How to sign in first; null when the route needs no sign-in.</summary>
    public LoginSpec? Login { get; init; }

    /// <summary>The point to click to drop focus, or null when the click is skipped.</summary>
    /// <returns>The point.</returns>
    public Offset? NeutralClickPoint() =>
        SkipNeutralClick ? null : NeutralClick ?? new Offset(Viewport.W - 22, Viewport.H - 22);
}
