using System;
using ClosedXML.Parser.Rolex;

namespace ClosedXML.Parser;

/// <summary>
/// Reads a formula written in one <see cref="ReferenceStyle"/>. An adapter binds the lexer
/// table to the token reader, so a formula cannot be lexed in one style and then have its
/// references read in another.
/// </summary>
internal interface IReferenceStyle
{
    /// <summary>
    /// The DFA table the lexer has to use for this reference style.
    /// </summary>
    DfaEntry[] DfaTable { get; }

    /// <summary>
    /// Extract the reference from a reference token.
    /// </summary>
    ReferenceArea ParseReference(ReadOnlySpan<char> token);

    /// <summary>
    /// Extract the called cell from a <c>CELL_FUNCTION_LIST</c> token.
    /// </summary>
    RowCol ParseCellFunction(ReadOnlySpan<char> token);
}
