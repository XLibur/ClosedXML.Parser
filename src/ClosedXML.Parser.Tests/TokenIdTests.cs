using System.Reflection;
using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser.Tests;

/// <summary>
/// The recursive descent parser, the Rolex lexer and the ANTLR lexer have to agree on the ID of each token.
/// </summary>
public class TokenIdTests
{
    [Fact]
    public void Token_ids_are_the_ids_of_the_ANTLR_lexer()
    {
        var antlrIds = TokenConstants(typeof(FormulaLexer));
        var tokenIds = TokenConstants(typeof(Token))
            .Where(x => x.Key is not nameof(Token.ErrorSymbolId) and not nameof(Token.EofSymbolId));

        foreach (var (name, id) in tokenIds)
        {
            Assert.True(antlrIds.TryGetValue(name, out var antlrId), $"The ANTLR lexer has no token {name}.");
            Assert.Equal(antlrId, id);
        }
    }

    [Fact]
    public void Both_Rolex_tables_number_the_tokens_alike()
    {
        Assert.Equal(TokenConstants(typeof(RolexA1Dfa)), TokenConstants(typeof(RolexR1C1Dfa)));
    }

    private static Dictionary<string, int> TokenConstants(Type type)
    {
        return type
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(int) && f.Name.ToUpperInvariant() == f.Name)
            .ToDictionary(f => f.Name, f => (int)f.GetValue(null)!);
    }
}
