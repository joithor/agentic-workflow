using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>
/// The top-level short forms of the 'workflow' verbs (engine spec rev 8 §6.3): 'sindri takeover' and 'sindri handback' take an item
/// or an address, as 'sindri workflow takeover' and 'sindri workflow handback' do. Run through the verb table exactly as Program does;
/// no address is given, so the verb answers with its usage before it asks for a control plane.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class ShortFormCliTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public ShortFormCliTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("takeover")]
    [InlineData("handback")]
    public async Task A_short_form_without_its_target_answers_with_its_own_usage_not_unknown_command(string verb)
    {
        var exit = await CommandTable.Run([verb], name => name == "AW_SINDRI_HOME" ? _home.Path : null, TimeProvider.System);

        var text = _out.ToString() + _error.ToString();
        Assert.Equal(2, exit);
        Assert.DoesNotContain("SND-CLI-001", text, StringComparison.Ordinal);
        Assert.Contains($"usage: sindri {verb} <item-or-address>", text, StringComparison.Ordinal);
    }
}
