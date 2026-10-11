namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The pages the browser tests drive.</summary>
internal static class FixtureSite
{
    internal static IReadOnlyDictionary<string, string> Pages(string elsewhere) => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["/"] = Home(elsewhere),
        ["/next"] = "<!doctype html><html><head><title>Next</title></head><body><h1>Next page</h1></body></html>",
        ["/controls"] = Lines(
            "<!doctype html><html><head><title>Controls</title></head><body>",
            "<form><input type=\"submit\" data-testid=\"submit-input\" value=\"Delete account\"></form>",
            "<button data-testid=\"icon\" title=\"Delete\"><svg width=\"16\" height=\"16\"></svg></button>",
            "</body></html>"),
        ["/tall"] = Lines(
            "<!doctype html><html><head><title>Tall</title></head><body>",
            "<div data-testid=\"top\">Top</div><div style=\"height: 3000px\"></div>",
            "<script>window.addEventListener('load', () => window.scrollTo(0, 500));</script>",
            "</body></html>"),
    };

    /// <summary>An app that sends a signed-out visitor from its root to /login, a two-step form; submitting goes to /home.</summary>
    internal static IReadOnlyDictionary<string, string> SignInApp() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["/"] = "<!doctype html><html><head><title>App</title><script>location.replace('/login');</script></head><body></body></html>",
        ["/login"] = Lines(
            "<!doctype html><html><head><title>Sign in</title></head><body>",
            "<input id=\"email\"><button id=\"next\" onclick=\"document.getElementById('password').style.display = 'inline'\">Next</button>",
            "<input id=\"password\" type=\"password\" style=\"display: none\">",
            "<button id=\"submit\" onclick=\"location.href = '/home'\">Sign in</button>",
            "</body></html>"),
        ["/home"] = "<!doctype html><html><head><title>Home</title></head><body><h1>Welcome</h1></body></html>",
    };

    private static string Home(string elsewhere) => Lines(
        "<!doctype html><html><head><title>Fixture</title><style>",
        "  button.styled:hover { color: red; } button.styled:focus { outline: 2px solid blue; }",
        "  #corner { position: fixed; left: 0; top: 0; }",
        "</style></head><body>",
        "<span style=\"display: none\">Hospitals</span><p>Hospitals</p>",
        "<input data-testid=\"search\" aria-label=\"Search\">",
        "<button data-testid=\"open\" class=\"styled\" onclick=\"document.getElementById('panel').style.display = 'block'\">Open</button>",
        "<div id=\"panel\" style=\"display: none\">Panel open</div>",
        "<button data-testid=\"hidden-button\" style=\"display: none\">Hidden</button>",
        "<button id=\"plain\">Plain</button>",
        "<a data-testid=\"next\" href=\"/next\">Next</a>",
        "<a data-testid=\"away\" href=\"" + elsewhere + "/away\">Away</a>",
        "<button id=\"corner\" data-testid=\"corner\" onclick=\"document.getElementById('corner-hit').style.display = 'block'\">Corner</button>",
        "<div id=\"corner-hit\" style=\"display: none\">Corner clicked</div>",
        "<div id=\"neutral\" style=\"display: none\">Neutral clicked</div>",
        "<script>",
        "  document.addEventListener('click', (e) => {",
        "    if (e.target === document.body || e.target === document.documentElement) document.getElementById('neutral').style.display = 'block';",
        "  });",
        "</script>",
        "</body></html>");

    private static string Lines(params string[] lines) => string.Join('\n', lines);
}
