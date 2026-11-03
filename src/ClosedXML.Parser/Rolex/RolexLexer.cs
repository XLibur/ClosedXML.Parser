using System.Collections.Generic;
using System;

namespace XLibur.Parser.Rolex;

internal class RolexLexer
{
    /// <summary>
    /// The most tokens a list can have room for and still be kept for the next lex. One huge formula
    /// shouldn't pin a large array for the life of a thread.
    /// </summary>
    internal const int MaxKeptTokens = 256;

    /// <summary>
    /// A token list the thread keeps for the next caller of <see cref="RentTokens"/>. It is null while
    /// a caller has it, so a caller that lexes again before it is done (a nested parse) gets a list
    /// of its own.
    /// </summary>
    [ThreadStatic]
    private static List<Token>? t_tokens;

    /// <summary>
    /// Get all tokens for a formula into a list the thread keeps, for a caller that drops the tokens
    /// once it has read them. The caller must give the list back with <see cref="ReturnTokens"/> and
    /// must not use it afterwards.
    /// </summary>
    internal static List<Token> RentTokens(ReadOnlySpan<char> formula, int[] lexerDfa)
    {
        var tokens = t_tokens ?? new List<Token>();
        t_tokens = null;
        GetTokens(formula, lexerDfa, tokens);
        return tokens;
    }

    /// <summary>
    /// Give back a list from <see cref="RentTokens"/>, so the next caller on the thread can use it.
    /// </summary>
    internal static void ReturnTokens(List<Token> tokens)
    {
        if (tokens.Capacity <= MaxKeptTokens)
            t_tokens = tokens;
    }

    /// <summary>
    /// Get all tokens for a formula. Use A1 semantic. If there is an error, add token with an error symbol at the end or EOF token at the end.
    /// </summary>
    /// <param name="formula">Formula to parse.</param>
    public static List<Token> GetTokensA1(ReadOnlySpan<char> formula)
    {
        return GetTokens(formula, RolexA1Dfa.DfaTable);
    }

    /// <summary>
    /// Get all tokens for a formula. Use R1C1 semantic. If there is an error, add token with an error symbol at the end or EOF token at the end.
    /// </summary>
    /// <param name="formula">Formula to parse.</param>
    public static List<Token> GetTokensR1C1(ReadOnlySpan<char> formula)
    {
        return GetTokens(formula, RolexR1C1Dfa.DfaTable);
    }

    internal static List<Token> GetTokens(ReadOnlySpan<char> formula, int[] lexerDfa)
    {
        var tokens = new List<Token>();
        GetTokens(formula, lexerDfa, tokens);
        return tokens;
    }

    /// <summary>
    /// Get all tokens for a formula into <paramref name="tokens"/>, replacing what the list held.
    /// </summary>
    internal static void GetTokens(ReadOnlySpan<char> formula, int[] lexerDfa, List<Token> tokens)
    {
        tokens.Clear();
        for (var i = 0; i < formula.Length;)
        {
            var (symbolId, length) = GetToken(formula, i, lexerDfa);
            tokens.Add(new Token(symbolId, i, length));
            if (symbolId < 0)
            {
                return;
            }

            i += length;
        }

        tokens.Add(Token.EofSymbol(formula.Length));
    }

    private static int Next(ReadOnlySpan<char> input, ref int index)
    {
        var c = input[index];

        // Only a high surrogate that is actually followed by a low surrogate forms a
        // codepoint. A trailing or mismatched surrogate is malformed UTF-16: step over the
        // single unit and return it as it stands. No token contains a surrogate on its own,
        // so the caller finds no transition for it and reports an error token, which is
        // what the try-parse entry points need in order to return false.
        if (char.IsHighSurrogate(c) &&
            index + 1 < input.Length &&
            char.IsLowSurrogate(input[index + 1]))
        {
            var codepoint = char.ConvertToUtf32(c, input[index + 1]);
            index += 2;
            return codepoint;
        }

        ++index;
        return c;
    }

    /// <summary>
    /// Read the longest token that starts at <paramref name="startIdx"/>.
    /// </summary>
    /// <param name="input">Formula to read from.</param>
    /// <param name="startIdx">Index of the first char of the token.</param>
    /// <param name="dfaTable">
    /// A table in the flat layout Rolex emits. A state starts at an offset and holds its accept
    /// symbol (-1 for none), its transition count, then each transition as a destination offset, a
    /// range count and that many (min, max) pairs, sorted by min. The start state is at offset 0.
    /// </param>
    internal static (int SymbolId, int Length) GetToken(ReadOnlySpan<char> input, int startIdx, int[] dfaTable)
    {
        var dfaState = 0;

        // Best token and its length found so far
        var symbolId = Token.ErrorSymbolId;
        var symbolEndIndex = 0;

        for (var idx = startIdx; idx < input.Length;)
        {
            var ch = Next(input, ref idx);

            var nextDfaState = FindTransition(dfaTable, dfaState, ch);

            // No valid transition was found from dfaState
            if (nextDfaState == -1)
            {
                // Return best symbolId. If nothing was yet found, it returns -2 as an error.
                if (symbolId < 0)
                    return (Token.ErrorSymbolId, 0);

                // Adjust symbol because ANTLR and Rolex are 1 off
                return (symbolId + 1, symbolEndIndex - startIdx);
            }

            // There is a valid transition.
            dfaState = nextDfaState;
            var currentSymbolId = dfaTable[dfaState];

            // The substring from startIdx to the current idx are a valid token
            if (currentSymbolId != -1)
            {
                symbolId = currentSymbolId;
                symbolEndIndex = idx;
            }
        }

        // We are at the end of the input. If there is a valid symbol, return it.
        if (symbolId >= 0)
        {
            // Adjust symbol because ANTLR and Rolex are 1 off
            return (symbolId + 1, symbolEndIndex - startIdx);
        }

        // Otherwise, end of stream, but the only way we could got here is if there has been a half-inserted
        // symbol, e.g. `"Text ` without the ending. This method is not called when lexer is at the end of
        // a stream, thus it has to be an error.
        return (Token.ErrorSymbolId, 0);
    }

    /// <summary>
    /// Find the offset of the state that <paramref name="ch"/> leads to from the state at
    /// <paramref name="state"/>, or -1 if no transition takes it.
    /// </summary>
    private static int FindTransition(int[] dfaTable, int state, int ch)
    {
        var transitionCount = dfaTable[state + 1];
        var pos = state + 2;
        for (var t = 0; t < transitionCount; ++t)
        {
            var destination = dfaTable[pos];
            var rangeCount = dfaTable[pos + 1];
            pos += 2;
            for (var r = 0; r < rangeCount; ++r)
            {
                // Ranges of a transition are sorted, so a char below this one is below the rest.
                if (ch < dfaTable[pos])
                {
                    pos += (rangeCount - r) * 2;
                    break;
                }

                if (ch <= dfaTable[pos + 1])
                    return destination;

                pos += 2;
            }
        }

        return -1;
    }
}
