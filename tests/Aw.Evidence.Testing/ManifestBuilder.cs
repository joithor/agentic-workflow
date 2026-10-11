using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Testing;

/// <summary>Builds a <see cref="DesignManifest"/>.</summary>
public sealed class ManifestBuilder
{
    private readonly List<DesignFrame> _frames = [];
    private LoginSpec? _login;

    /// <summary>A frame named <paramref name="name"/> anchored on test id 'card', region 0,0 10x10.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The frame.</returns>
    public static DesignFrame Frame(string name = "default") =>
        new(name, $"/designs/{name}.png", new Region(0, 0, 10, 10), ElementSelector.ByTestId(TestId.From("card")));

    /// <summary>A sign-in spec reading APP_EMAIL and APP_PASSWORD.</summary>
    /// <returns>The spec.</returns>
    public static LoginSpec Login() => new("APP_EMAIL", "APP_PASSWORD", "#email", "#password", "button[type=submit]");

    /// <summary>Adds a frame.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>This builder.</returns>
    public ManifestBuilder WithFrame(DesignFrame frame)
    {
        _frames.Add(frame);
        return this;
    }

    /// <summary>Signs in first.</summary>
    /// <param name="login">The spec.</param>
    /// <returns>This builder.</returns>
    public ManifestBuilder WithLogin(LoginSpec login)
    {
        _login = login;
        return this;
    }

    /// <summary>Builds the manifest (one default frame when none were added).</summary>
    /// <returns>The manifest.</returns>
    public DesignManifest Build() =>
        new("/hospitals", ElementSelector.ByText("Hospitals"), _frames.Count > 0 ? [.. _frames] : [Frame()], login: _login);
}
