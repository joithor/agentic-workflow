namespace Aw.Sindri.Ui.Testing;

/// <summary>What the API answers on the session reads (collector spec CA-1, CA-2), taken from its goldens on disk.</summary>
internal static class SessionAnswers
{
    internal static RouteAnswer List() => throw new NotSupportedException();

    internal static RouteAnswer One(string provider, string id) => throw new NotSupportedException();
}
