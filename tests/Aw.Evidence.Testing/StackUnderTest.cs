using Aw.Evidence.Core.Stack;

namespace Aw.Evidence.Testing;

/// <summary>A probe over an arranged stack, with where its login page and probed port are.</summary>
/// <param name="Probe">The probe under test.</param>
/// <param name="LoginPage">The login page's URL.</param>
/// <param name="Host">The probed port's host.</param>
/// <param name="Port">The probed port.</param>
public sealed record StackUnderTest(IStackProbe Probe, Uri LoginPage, string Host, int Port);
