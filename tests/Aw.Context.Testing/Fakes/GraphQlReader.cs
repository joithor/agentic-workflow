using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// Reads just enough GraphQL to answer a read: the operation's root fields, their aliases and arguments. Argument
/// values are variables or scalar literals; objects and lists must come through variables. Sub-selections are skipped
/// (the fake answers with whole recorded nodes).
/// </summary>
internal sealed class GraphQlReader(string text, JsonObject? variables)
{
    private int _at;

    public static IReadOnlyList<GraphQlField> RootFields(string query, JsonObject? variables) =>
        new GraphQlReader(query, variables).Operation();

    private char Peek => _at < text.Length ? text[_at] : '\0';

    private List<GraphQlField> Operation()
    {
        SkipIgnored();
        if (Peek != '{')
        {
            OperationHeader();
        }

        Expect('{');
        var fields = new List<GraphQlField>();
        for (SkipIgnored(); Peek != '}'; SkipIgnored())
        {
            fields.Add(Field());
        }

        return fields;
    }

    private void OperationHeader()
    {
        var kind = Name();
        if (string.Equals(kind, "mutation", StringComparison.Ordinal) || string.Equals(kind, "subscription", StringComparison.Ordinal))
        {
            throw FakeRequestException.Invalid($"The fake Linear server is read-only: '{kind}' operations aren't served.");
        }

        if (kind != "query")
        {
            throw FakeRequestException.Invalid($"Expected 'query', 'mutation' or '{{' at the start of the document, found '{kind}'.");
        }

        SkipIgnored();
        if (char.IsLetter(Peek) || Peek == '_')
        {
            _ = Name();
            SkipIgnored();
        }

        if (Peek == '(')
        {
            SkipBalanced('(', ')');
            SkipIgnored();
        }
    }

    private GraphQlField Field()
    {
        if (Peek == '.')
        {
            throw FakeRequestException.Invalid("Fragment spreads at the root aren't supported by the fake Linear server; select the root fields directly.");
        }

        var key = Name();
        var name = key;
        SkipIgnored();
        if (Peek == ':')
        {
            _at++;
            SkipIgnored();
            name = Name();
            SkipIgnored();
        }

        var arguments = Peek == '(' ? Arguments() : new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        SkipIgnored();
        if (Peek == '{')
        {
            SkipBalanced('{', '}');
        }

        return new GraphQlField(key, name, arguments);
    }

    private Dictionary<string, JsonNode?> Arguments()
    {
        Expect('(');
        var arguments = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        for (SkipIgnored(); Peek != ')'; SkipIgnored())
        {
            var name = Name();
            SkipIgnored();
            Expect(':');
            SkipIgnored();
            arguments[name] = Value(name);
        }

        _at++;
        return arguments;
    }

    private JsonNode? Value(string argument)
    {
        if (Peek == '$')
        {
            _at++;
            return variables?[Name()]?.DeepClone();
        }

        if (Peek is '{' or '[')
        {
            throw FakeRequestException.Invalid($"Argument '{argument}': pass object and list values through variables; the fake Linear server reads only scalar literals inline.");
        }

        return Peek == '"' ? JsonValue.Create(StringLiteral()) : ScalarLiteral();
    }

    private JsonValue? ScalarLiteral()
    {
        var start = _at;
        while (char.IsLetterOrDigit(Peek) || Peek is '_' or '-' or '.')
        {
            _at++;
        }

        var token = text[start.._at];
        return token switch
        {
            "true" => JsonValue.Create(value: true),
            "false" => JsonValue.Create(value: false),
            "null" => null,
            _ when int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
            _ when token.Length > 0 => JsonValue.Create(token),
            _ => throw FakeRequestException.Invalid(string.Create(CultureInfo.InvariantCulture, $"Unexpected '{Peek}' at offset {_at} of the query.")),
        };
    }

    private string StringLiteral()
    {
        var value = new StringBuilder();
        for (_at++; Peek != '"'; _at++)
        {
            if (Peek == '\0')
            {
                throw FakeRequestException.Invalid("Unterminated string in the query.");
            }

            _ = value.Append(Peek == '\\' ? text[++_at] : Peek);
        }

        _at++;
        return value.ToString();
    }

    private string Name()
    {
        var start = _at;
        while (char.IsLetterOrDigit(Peek) || Peek == '_')
        {
            _at++;
        }

        return _at > start ? text[start.._at] : throw FakeRequestException.Invalid(string.Create(CultureInfo.InvariantCulture, $"Expected a name at offset {start} of the query."));
    }

    private void Expect(char c)
    {
        if (Peek != c)
        {
            throw FakeRequestException.Invalid(string.Create(CultureInfo.InvariantCulture, $"Expected '{c}' at offset {_at} of the query."));
        }

        _at++;
    }

    private void SkipBalanced(char open, char close)
    {
        var depth = 0;
        do
        {
            if (Peek == '"')
            {
                _ = StringLiteral();
                continue;
            }

            depth += Peek == open ? 1 : Peek == close ? -1 : 0;
            _at = Peek == '\0' ? throw FakeRequestException.Invalid($"Unbalanced '{open}' in the query.") : _at + 1;
        }
        while (depth > 0);
    }

    private void SkipIgnored()
    {
        while (char.IsWhiteSpace(Peek) || Peek is ',' or '#')
        {
            if (Peek == '#')
            {
                while (Peek is not ('\n' or '\0'))
                {
                    _at++;
                }
            }
            else
            {
                _at++;
            }
        }
    }
}
