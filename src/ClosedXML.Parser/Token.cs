using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ClosedXML.Parser.Rolex;

// ReSharper disable InconsistentNaming
namespace ClosedXML.Parser;

/// <summary>
/// A token for a formula input.
/// </summary>
internal readonly struct Token
{
    private static readonly IReadOnlyDictionary<int, string> SymbolNames = typeof(Token)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(int) && f.IsLiteral)
        .ToDictionary(x => (int)x.GetValue(null), x => x.Name);

    /// <summary>
    /// An error symbol id.
    /// </summary>
    public const int ErrorSymbolId = -2;

    /// <summary>
    /// An symbol id for end of file. Mostly for compatibility with ANTLR.
    /// </summary>
    public const int EofSymbolId = -1;

    // The IDs are the ones of the ANTLR lexer, which numbers the tokens from 1 (see FormulaLexer.tokens).
    // The Rolex tables are generated from the same grammar, but number the tokens from 0, and the lexer
    // adds one to what a table accepts. Deriving each ID from the generated constant keeps this list in
    // step with the grammar. Both tables number the tokens alike, so the A1 one stands for both.
    public const int REF_CONSTANT = RolexA1Dfa.REF_CONSTANT + 1;
    public const int NONREF_ERRORS = RolexA1Dfa.NONREF_ERRORS + 1;
    public const int LOGICAL_CONSTANT = RolexA1Dfa.LOGICAL_CONSTANT + 1;
    public const int NUMERICAL_CONSTANT = RolexA1Dfa.NUMERICAL_CONSTANT + 1;
    public const int STRING_CONSTANT = RolexA1Dfa.STRING_CONSTANT + 1;
    public const int POW = RolexA1Dfa.POW + 1;
    public const int MULT = RolexA1Dfa.MULT + 1;
    public const int DIV = RolexA1Dfa.DIV + 1;
    public const int PLUS = RolexA1Dfa.PLUS + 1;
    public const int MINUS = RolexA1Dfa.MINUS + 1;
    public const int CONCAT = RolexA1Dfa.CONCAT + 1;
    public const int EQUAL = RolexA1Dfa.EQUAL + 1;
    public const int NOT_EQUAL = RolexA1Dfa.NOT_EQUAL + 1;
    public const int LESS_OR_EQUAL_THAN = RolexA1Dfa.LESS_OR_EQUAL_THAN + 1;
    public const int LESS_THAN = RolexA1Dfa.LESS_THAN + 1;
    public const int GREATER_OR_EQUAL_THAN = RolexA1Dfa.GREATER_OR_EQUAL_THAN + 1;
    public const int GREATER_THAN = RolexA1Dfa.GREATER_THAN + 1;
    public const int PERCENT = RolexA1Dfa.PERCENT + 1;
    public const int SEMICOLON = RolexA1Dfa.SEMICOLON + 1;
    public const int COLON = RolexA1Dfa.COLON + 1;
    public const int OPEN_BRACE = RolexA1Dfa.OPEN_BRACE + 1;
    public const int CLOSE_BRACE = RolexA1Dfa.CLOSE_BRACE + 1;
    public const int OPEN_CURLY = RolexA1Dfa.OPEN_CURLY + 1;
    public const int CLOSE_CURLY = RolexA1Dfa.CLOSE_CURLY + 1;
    public const int COMMA = RolexA1Dfa.COMMA + 1;
    public const int SPACE = RolexA1Dfa.SPACE + 1;
    public const int INTERSECT = RolexA1Dfa.INTERSECT + 1;
    public const int SPILL = RolexA1Dfa.SPILL + 1;
    public const int BOOK_PREFIX = RolexA1Dfa.BOOK_PREFIX + 1;
    public const int BANG_REFERENCE = RolexA1Dfa.BANG_REFERENCE + 1;
    public const int SHEET_RANGE_PREFIX = RolexA1Dfa.SHEET_RANGE_PREFIX + 1;
    public const int SINGLE_SHEET_PREFIX = RolexA1Dfa.SINGLE_SHEET_PREFIX + 1;
    public const int A1_CELL = RolexA1Dfa.A1_CELL + 1;
    public const int A1_SPAN_REFERENCE = RolexA1Dfa.A1_SPAN_REFERENCE + 1;
    public const int REF_FUNCTION_LIST = RolexA1Dfa.REF_FUNCTION_LIST + 1;
    public const int CELL_FUNCTION_LIST = RolexA1Dfa.CELL_FUNCTION_LIST + 1;
    public const int USER_DEFINED_FUNCTION_NAME = RolexA1Dfa.USER_DEFINED_FUNCTION_NAME + 1;
    public const int NAME = RolexA1Dfa.NAME + 1;
    public const int INTRA_TABLE_REFERENCE = RolexA1Dfa.INTRA_TABLE_REFERENCE + 1;
    public const int DDE_ITEM = RolexA1Dfa.DDE_ITEM + 1;
    public const int BANG_NAME = RolexA1Dfa.BANG_NAME + 1;

    /// <summary>
    /// A token ID or TokenType. Non-negative integer. The values are from Antlr grammar, starting with 1.
    /// See <c>FormulaLexer.tokens</c>. The value -1 indicates an error and unrecognized token and is always
    /// last token.
    /// </summary>
    public readonly int SymbolId;

    /// <summary>
    /// The starting index of a token, in code units (=chars).
    /// </summary>
    public readonly int StartIndex;

    /// <summary>
    /// Length of a token in code units (=chars). For non-error tokens, must be at least 1. Ignore for error token.
    /// </summary>
    public readonly int Length;

    public Token(int symbolId, int startIndex, int length)
    {
        SymbolId = symbolId;
        StartIndex = startIndex;
        Length = length;
    }

    public static Token EofSymbol(int index) => new(EofSymbolId, index, 0);

    public static string GetSymbolName(int symbolId)
    {
        if (!SymbolNames.TryGetValue(symbolId, out var name))
            throw new ArgumentOutOfRangeException($"Invalid symbol {symbolId}.");

        return name;
    }

    public bool Equals(Token other)
    {
        return SymbolId == other.SymbolId && StartIndex == other.StartIndex && Length == other.Length;
    }

    public override bool Equals(object? obj)
    {
        return obj is Token other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = SymbolId;
            hashCode = (hashCode * 397) ^ StartIndex;
            hashCode = (hashCode * 397) ^ Length;
            return hashCode;
        }
    }

    public override string ToString() => $"Symbol: {SymbolId}; StartIdx: {StartIndex}; Len: {Length}";
}