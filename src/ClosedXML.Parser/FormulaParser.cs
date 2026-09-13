using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Parser.Rolex;
using JetBrains.Annotations;

namespace ClosedXML.Parser;

/// <summary>
/// A parser of Excel formulas, with main purpose of creating an abstract syntax tree.
/// </summary>
/// <remarks>
/// An implementation is a recursive descent parser, based on the ANTLR grammar.
/// </remarks>
/// <typeparam name="TScalarValue">Type of a scalar value used across expressions.</typeparam>
/// <typeparam name="TNode">Type of a node used in the AST.</typeparam>
/// <typeparam name="TContext">A context of the parsing. It's passed to every factory method and can contain global info that doesn't belong individual nodes.</typeparam>
[PublicAPI]
public class FormulaParser<TScalarValue, TNode, TContext>
{
    private const string REF_ERROR = "#REF!";
    private readonly string _input;
    private readonly List<Token> _tokens;
    private readonly IAstFactory<TScalarValue, TNode, TContext> _factory;
    private readonly TContext _context;

    /// <summary>
    /// Reads the tokens and the references of the formula, in the style it is written in.
    /// </summary>
    private readonly IReferenceStyle _style;
    private Token _tokenSource;
    private int _tokenIndex = -1;

    // Current lookahead token index
    private int _la;

    private FormulaParser(string formula, TContext context, IAstFactory<TScalarValue, TNode, TContext> factory, bool a1Mode)
    {
        // Trim the end, so ref_intersection_expression that tried to parse SPACE as an operator
        // doesn't recognize spaces at the end of formula as operators. The control tokens of
        // the formula have whitespaces around them (unlike params), so the whitespaces should
        // be consumed by control tokens (e.g. ` IF ( A1 ) ` will be split into `IF ( `, `A1` and ` ) `)
        // but to avoid the whitespace at the end, trim it.
        var trimmedFormula = formula.AsSpan().TrimEnd();
        _input = formula;
        _context = context;
        // The DFA table and the reference reader have to be of the same style, so take both
        // from one adapter instead of deciding the style twice.
        _style = a1Mode ? TokenParser.A1Style : TokenParser.R1C1Style;
        _tokens = RolexLexer.GetTokens(trimmedFormula, _style.DfaTable);
        _factory = factory;
        Consume();
    }

    /// <summary>
    /// Parse a formula using A1 semantic for references. 
    /// </summary>
    /// <param name="formula">Formula text that will be parsed.</param>
    /// <param name="context">Context that is going to be passed to every method of the <paramref name="factory"/>.</param>
    /// <param name="factory">Factory to create nodes of AST tree.</param>
    /// <exception cref="ParsingException">If the formula doesn't satisfy the grammar.</exception>
    public static TNode CellFormulaA1(string formula, TContext context, IAstFactory<TScalarValue, TNode, TContext> factory)
    {
        var parser = new FormulaParser<TScalarValue, TNode, TContext>(formula, context, factory, true);
        return parser.Formula();
    }

    /// <summary>
    /// Parse a formula using R1C1 semantic for references. 
    /// </summary>
    /// <param name="formula">Formula text that will be parsed.</param>
    /// <param name="context">Context that is going to be passed to every method of the <paramref name="factory"/>.</param>
    /// <param name="factory">Factory to create nodes of AST tree.</param>
    /// <exception cref="ParsingException">If the formula doesn't satisfy the grammar.</exception>
    public static TNode CellFormulaR1C1(string formula, TContext context, IAstFactory<TScalarValue, TNode, TContext> factory)
    {
        var parser = new FormulaParser<TScalarValue, TNode, TContext>(formula, context, factory, false);
        return parser.Formula();
    }

    private TNode Formula()
    {
        if (_tokens[_tokens.Count - 1].SymbolId == Token.ErrorSymbolId)
            throw new ParsingException($"Unable to determine token for '{_input}' at index {_tokens[_tokens.Count - 1].StartIndex}.");

        if (_la == Token.SPACE)
            Consume();
        var expression = Expression(false, out _);
        if (_la != Token.EofSymbolId)
        {
            var parsedPart = _input.Substring(0, _tokenSource.StartIndex);
            var remainder = _input.Substring(_tokenSource.StartIndex);
            throw new ParsingException($"The formula `{_input}` wasn't parsed correctly. The expression `{parsedPart}` was parsed, but the rest `{remainder}` wasn't.");
        }

        return expression;
    }

    private TNode Expression(bool skipRangeUnion, out bool isPureRef)
    {
        var start = _tokenSource.StartIndex;
        var leftNode = ConcatExpression(skipRangeUnion, out isPureRef);
        while (true)
        {
            BinaryOperation cmpOp;
            switch (_la)
            {
                case Token.GREATER_OR_EQUAL_THAN:
                    cmpOp = BinaryOperation.GreaterOrEqualThan;
                    break;
                case Token.LESS_OR_EQUAL_THAN:
                    cmpOp = BinaryOperation.LessOrEqualThan;
                    break;
                case Token.LESS_THAN:
                    cmpOp = BinaryOperation.LessThan;
                    break;
                case Token.GREATER_THAN:
                    cmpOp = BinaryOperation.GreaterThan;
                    break;
                case Token.NOT_EQUAL:
                    cmpOp = BinaryOperation.NotEqual;
                    break;
                case Token.EQUAL:
                    cmpOp = BinaryOperation.Equal;
                    break;
                default:
                    return leftNode;
            }

            Consume();
            isPureRef = false;

            var rightNode = ConcatExpression(skipRangeUnion, out _);
            leftNode = _factory.BinaryNode(_context, new SymbolRange(start, _tokenSource.StartIndex), cmpOp, leftNode, rightNode);
        }
    }

    private TNode ConcatExpression(bool skipRangeUnion, out bool isPureRef)
    {
        var start = _tokenSource.StartIndex;
        var leftNode = AdditiveExpression(skipRangeUnion, out isPureRef);
        while (_la == Token.CONCAT)
        {
            Consume();
            isPureRef = false;
            var rightNode = AdditiveExpression(skipRangeUnion, out _);
            leftNode = _factory.BinaryNode(_context, new SymbolRange(start, _tokenSource.StartIndex), BinaryOperation.Concat, leftNode, rightNode);
        }

        return leftNode;
    }
    private TNode AdditiveExpression(bool skipRangeUnion, out bool isPureRef)
    {
        var start = _tokenSource.StartIndex;
        var leftNode = MultiplyingExpression(skipRangeUnion, out isPureRef);
        while (true)
        {
            BinaryOperation op;
            switch (_la)
            {
                case Token.PLUS:
                    op = BinaryOperation.Addition;
                    break;
                case Token.MINUS:
                    op = BinaryOperation.Subtraction;
                    break;
                default:
                    return leftNode;
            }

            Consume();
            isPureRef = false;
            var rightNode = MultiplyingExpression(skipRangeUnion, out _);
            leftNode = _factory.BinaryNode(_context, new SymbolRange(start, _tokenSource.StartIndex), op, leftNode, rightNode);
        }
    }

    private TNode MultiplyingExpression(bool skipRangeUnion, out bool isPureRef)
    {
        var start = _tokenSource.StartIndex;
        var leftNode = PowExpression(skipRangeUnion, out isPureRef);
        while (true)
        {
            BinaryOperation op;
            switch (_la)
            {
                case Token.MULT:
                    op = BinaryOperation.Multiplication;
                    break;
                case Token.DIV:
                    op = BinaryOperation.Division;
                    break;
                default:
                    return leftNode;
            }

            Consume();
            isPureRef = false;
            var appendNode = PowExpression(skipRangeUnion, out _);
            leftNode = _factory.BinaryNode(_context, new SymbolRange(start, _tokenSource.StartIndex), op, leftNode, appendNode);
        }
    }

    private TNode PowExpression(bool skipRangeUnion, out bool isPureRef)
    {
        var start = _tokenSource.StartIndex;
        var leftNode = PercentExpression(skipRangeUnion, out isPureRef);
        while (_la == Token.POW)
        {
            Consume();
            isPureRef = false;
            var rightNode = PercentExpression(skipRangeUnion, out _);
            leftNode = _factory.BinaryNode(_context, new SymbolRange(start, _tokenSource.StartIndex), BinaryOperation.Power, leftNode, rightNode);
        }

        return leftNode;
    }

    private TNode PercentExpression(bool skipRangeUnion, out bool isPureRef)
    {
        var start = _tokenSource.StartIndex;
        var prefixAtomNode = PrefixAtomExpression(skipRangeUnion, out isPureRef);
        var percentNode = prefixAtomNode;
        while (_la == Token.PERCENT)
        {
            Consume();
            isPureRef = false;
            percentNode = _factory.Unary(_context, new SymbolRange(start, _tokenSource.StartIndex), UnaryOperation.Percent, percentNode);
        }

        return percentNode;
    }

    /// <summary>
    /// Parser for two rules unified into a single method.
    /// <para>
    /// <c>
    /// prefix_atom_expression
    ///     : (PLUS | MINUS) prefix_atom_expression
    ///     | atom_expression
    ///     ;
    /// </c>
    /// 
    /// <c>
    /// arg_prefix_atom_expression
    ///     : (PLUS | MINUS) arg_prefix_atom_expression
    ///     | arg_atom_expression
    ///     ;     
    /// </c>
    /// </para>
    /// </summary>
    /// <param name="skipRangeUnion">Does the method represent <c>prefix_atom_expression</c> (<c>false</c>) or <c>arg_prefix_atom_expression</c> (<c>true</c>)</param>
    /// <param name="isPureRef">Is the expression of the node a reference expression?</param>
    /// <returns></returns>
    private TNode PrefixAtomExpression(bool skipRangeUnion, out bool isPureRef)
    {
        var start = _tokenSource.StartIndex;
        UnaryOperation op;
        switch (_la)
        {
            case Token.PLUS:
                op = UnaryOperation.Plus;
                break;
            case Token.MINUS:
                op = UnaryOperation.Minus;
                break;
            default:
                return AtomExpression(skipRangeUnion, out isPureRef);
        }

        Consume();
        var neutralAtom = PrefixAtomExpression(skipRangeUnion, out _);
        isPureRef = false;
        return _factory.Unary(_context, new SymbolRange(start, _tokenSource.StartIndex), op, neutralAtom);
    }

    private TNode AtomExpression(bool skipRangeUnion, out bool isPureRef)
    {
        switch (_la)
        {
            // Constant
            case Token.NONREF_ERRORS:
            case Token.LOGICAL_CONSTANT:
            case Token.NUMERICAL_CONSTANT:
            case Token.STRING_CONSTANT:
            case Token.OPEN_CURLY:
                isPureRef = false;
                var constantNode = Constant();
                return constantNode;

            // '(' expression ')'
            case Token.OPEN_BRACE:
                {
                    var start = _tokenSource.StartIndex;
                    Consume();
                    var expression = Expression(false, out isPureRef);
                    Match(Token.CLOSE_BRACE);
                    var nestedNode = _factory.Nested(_context, new SymbolRange(start, _tokenSource.StartIndex), expression);

                    // This is the point of an ambiguity. Atom should be a value, but it can
                    // be determined by calling an expression inside the braces or
                    // it can go through ref_expression path that also has an ref_expression
                    // in braces. The second option should be seriously rare, so parser
                    // tracks whether expression is a ref_expression and if it is, we 'backtrack'
                    // through patching to the correct path of the 'ref_expression' below.
                    // Example: '(A1) A1:B2' <- the '(A1)' should be detected as ref_expression
                    // and thus the ' ' intersection operator be valid. Of course, braces can be
                    // very nested '(((A1))) A1:B2' and when we are entering the brace, there is
                    // no way to detect whether it is ref_expression or expression.
                    if (isPureRef)
                    {
                        // Incorrect expectation, backtrack to the ref_expression. The node is passed
                        // on with the index it starts at, because the parser has read past it.
                        var readAtom = new ReadAtom(nestedNode, start);
                        if (skipRangeUnion)
                            return RefImplicitExpression(readAtom);

                        return RefExpression(readAtom);
                    }

                    return nestedNode;
                }

            // function_call
            case Token.CELL_FUNCTION_LIST:
                {
                    isPureRef = false;
                    if (TokenParser.IsFunctionNamedLikeCell(_input.AsSpan(), _tokenSource))
                        return LocalFunctionCall();

                    var start = _tokenSource.StartIndex;
                    var cellReference = _style.ParseCellFunction(_input.AsSpan(), _tokenSource);
                    Consume();
                    var args = ArgumentList();
                    var range = new SymbolRange(start, _tokenSource.StartIndex);
                    return _factory.CellFunction(_context, range, cellReference, args);
                }

            case Token.USER_DEFINED_FUNCTION_NAME:
                isPureRef = false;
                return LocalFunctionCall();

            default:
                // function_call : SINGLE_SHEET_PREFIX USER_DEFINED_FUNCTION_NAME argument_list
                if (_la == Token.SINGLE_SHEET_PREFIX && LL(1) == Token.USER_DEFINED_FUNCTION_NAME)
                {
                    isPureRef = false;
                    var start = _tokenSource.StartIndex;
                    var prefix = SheetPrefix.ReadSingle(_input.AsSpan(), _tokenSource);
                    Consume();
                    var functionName = TokenParser.ParseFunctionName(_input.AsSpan(), _tokenSource);
                    Consume();
                    var args = ArgumentList();
                    var range = new SymbolRange(start, _tokenSource.StartIndex);
                    return prefix.BookIndex is null
                        ? _factory.Function(_context, range, prefix.FirstSheet!, functionName, args)
                        : _factory.ExternalFunction(_context, range, prefix.BookIndex.Value, prefix.FirstSheet!, functionName, args);
                }

                // function_call : BOOK_PREFIX USER_DEFINED_FUNCTION_NAME argument_list
                if (_la == Token.BOOK_PREFIX && LL(1) == Token.USER_DEFINED_FUNCTION_NAME)
                {
                    isPureRef = false;
                    var start = _tokenSource.StartIndex;
                    var wbIndex = SheetPrefix.ReadBookPrefix(_input.AsSpan(), _tokenSource);
                    Consume();
                    var functionName = TokenParser.ParseFunctionName(_input.AsSpan(), _tokenSource);
                    Consume();
                    var args = ArgumentList();
                    var range = new SymbolRange(start, _tokenSource.StartIndex);
                    return _factory.ExternalFunction(_context, range, wbIndex, functionName, args);
                }

                // ref_expression
                if (skipRangeUnion)
                {
                    isPureRef = true;
                    return RefImplicitExpression();
                }

                isPureRef = true;
                return RefExpression();
        }
    }

    private TNode RefExpression(ReadAtom? readAtom = null)
    {
        var start = readAtom?.Start ?? _tokenSource.StartIndex;
        var leftNode = RefImplicitExpression(readAtom);
        while (_la == Token.COMMA)
        {
            Consume();
            var rightNode = RefImplicitExpression();
            leftNode = _factory.BinaryNode(_context, new SymbolRange(start, _tokenSource.StartIndex), BinaryOperation.Union, leftNode, rightNode);
        }

        return leftNode;
    }

    /// <summary>
    /// <code>
    /// ref_implicit_expression
    ///        : INTERSECT ref_implicit_expression
    ///        | ref_intersection_expression
    ///        ;
    /// </code>
    /// The <c>@</c> binds looser than the range and the intersection operators, so its operand is the rest of the
    /// intersection, e.g. <c>@A1:A10 A5</c> is <c>@(A1:A10 A5)</c>.
    /// </summary>
    private TNode RefImplicitExpression(ReadAtom? readAtom = null)
    {
        var start = readAtom?.Start ?? _tokenSource.StartIndex;

        // The atom was read before the parser backtracked, so an '@' after it can't be its prefix.
        if (readAtom is null && _la == Token.INTERSECT)
        {
            Consume();
            var refNode = RefImplicitExpression();
            return _factory.Unary(_context, new SymbolRange(start, _tokenSource.StartIndex), UnaryOperation.ImplicitIntersection, refNode);
        }

        return RefIntersectionExpression(readAtom);
    }

    /// <summary>
    /// <code>
    /// ref_intersection_expression
    ///     : ref_range_expression ((SPACE | {space at end of previous token}?) ref_range_expression)* ({space before @}? INTERSECT ref_implicit_expression)?
    ///     ;
    /// </code>
    /// </summary>
    private TNode RefIntersectionExpression(ReadAtom? readAtom = null)
    {
        var start = readAtom?.Start ?? _tokenSource.StartIndex;
        var leftNode = RefRangeExpression(readAtom);

        // The lexer puts the whitespace after an operator into its token, so the space of `(A1) B2` is the end of
        // the `) ` CLOSE_BRACE token and there is no SPACE token for the operator. The space is the operator only
        // when a ref_atom_expression follows, e.g. the space of `(A1) + B2` belongs to no operator.
        while (_la == Token.SPACE || (StartsRefAtomExpression(_la) && IsSpaceAfterPreviousToken()))
        {
            if (_la == Token.SPACE)
                Consume();

            var rightNode = RefRangeExpression();
            leftNode = _factory.BinaryNode(_context, new SymbolRange(start, _tokenSource.StartIndex), BinaryOperation.Intersection, leftNode, rightNode);
        }

        // The lexer puts the space before '@' into the INTERSECT token, so the token is the intersection operator
        // too. The implicit intersection takes the rest of the intersection, so nothing follows it here.
        if (_la == Token.INTERSECT && TokenParser.IsSpaceBeforeAt(_input.AsSpan(), _tokenSource))
        {
            var rightNode = RefImplicitExpression();
            leftNode = _factory.BinaryNode(_context, new SymbolRange(start, _tokenSource.StartIndex), BinaryOperation.Intersection, leftNode, rightNode);
        }

        return leftNode;
    }

    /// <summary>
    /// <code>
    /// ref_range_expression
    ///     : ref_spill_expression (COLON ref_spill_expression)* (COLON INTERSECT ref_implicit_expression)?
    ///     ;
    /// </code>
    /// </summary>
    private TNode RefRangeExpression(ReadAtom? readAtom = null)
    {
        var start = readAtom?.Start ?? _tokenSource.StartIndex;
        var leftNode = RefSpillExpression(readAtom);
        while (_la == Token.COLON)
        {
            Consume();

            // The implicit intersection takes the rest of the intersection, so it ends the range.
            var rightNode = _la == Token.INTERSECT ? RefImplicitExpression() : RefSpillExpression();
            leftNode = _factory.BinaryNode(_context, new SymbolRange(start, _tokenSource.StartIndex), BinaryOperation.Range, leftNode, rightNode);
        }

        return leftNode;
    }

    /// <summary>
    /// Parser of the following node.
    /// <c>
    /// ref_spill_expression
    ///     : ref_atom_expression SPILL?
    ///     ;
    /// </c>
    /// </summary>
    private TNode RefSpillExpression(ReadAtom? readAtom = null)
    {
        var start = readAtom?.Start ?? _tokenSource.StartIndex;
        var refAtomNode = RefAtomExpression(readAtom);
        if (_la == Token.SPILL)
        {
            Consume();
            return _factory.Unary(_context, new SymbolRange(start, _tokenSource.StartIndex), UnaryOperation.SpillRange, refAtomNode);
        }

        return refAtomNode;
    }

    private TNode RefAtomExpression(ReadAtom? readAtom = null)
    {
        // A backtracking of an incorrect detection whether an expression in a braces is value expression or ref expression.
        if (readAtom is not null)
            return readAtom.Value.Node;

        switch (_la)
        {
            // REF_CONSTANT (a1_reference | REF_CONSTANT)?
            // -> REF_CONSTANT
            // -> REF_CONSTANT REF_CONSTANT
            // -> REF_CONSTANT A1_CELL
            // -> REF_CONSTANT A1_CELL COLON A1_CELL
            // -> REF_CONSTANT A1_SPAN_REFERENCE
            // Happens when sheet is deleted, e.g. `#REF!A1`. Note that #REF is actually a valid
            // name of a sheet, but it must be escaped to be usable ('#REF'!B3) because of '#'.
            case Token.REF_CONSTANT:
                {
                    // In all cases, it is a #REF! error from AST PoV, just with weird tokens.
                    var start = _tokenSource.StartIndex;
                    var errorToken = GetCurrentToken();
                    Span<char> normalizedError = stackalloc char[errorToken.Length];
                    errorToken.ToUpperInvariant(normalizedError);
                    Match(Token.REF_CONSTANT);

                    if (_la == Token.REF_CONSTANT)
                    {
                        // -> REF_CONSTANT REF_CONSTANT
                        Match(Token.REF_CONSTANT);
                    }
                    else if (_la == Token.A1_CELL)
                    {
                        // -> REF_CONSTANT A1_CELL
                        Match(Token.A1_CELL);
                        if (_la == Token.COLON && LL(1) == Token.A1_CELL)
                        {
                            // -> REF_CONSTANT A1_CELL COLON A1_CELL
                            Match(Token.COLON);
                            Match(Token.A1_CELL);
                        }
                    }
                    else if (_la == Token.A1_SPAN_REFERENCE)
                    {
                        // -> REF_CONSTANT A1_SPAN_REFERENCE
                        Match(Token.A1_SPAN_REFERENCE);
                    }

                    return _factory.ErrorNode(_context, new SymbolRange(start, _tokenSource.StartIndex), normalizedError);
                }

            case Token.OPEN_BRACE:
                {
                    var start = _tokenSource.StartIndex;
                    Consume();
                    var refExpression = RefExpression();
                    Match(Token.CLOSE_BRACE);
                    return _factory.Nested(_context, new SymbolRange(start, _tokenSource.StartIndex), refExpression);
                }
            // cell_reference has been inlined into this switch

            // cell_reference:
            //     (A1_CELL | A1_CELL COLON A1_CELL | A1_SPAN_REFERENCE) -- inlined a1_reference
            case Token.A1_CELL:
            case Token.A1_SPAN_REFERENCE:
                {
                    var start = _tokenSource.StartIndex;
                    var area = A1Reference()!.Value;
                    return _factory.Reference(_context, new SymbolRange(start, _tokenSource.StartIndex), area);
                }

            // cell_reference:
            //     BANG_REFERENCE
            case Token.BANG_REFERENCE:
                {
                    var start = _tokenSource.StartIndex;
                    var isReference = TokenParser.TryParseBangReference(_style, _input.AsSpan(), _tokenSource, out var reference);
                    Consume();
                    var range = new SymbolRange(start, _tokenSource.StartIndex);
                    return isReference
                        ? _factory.BangReference(_context, range, reference)
                        : _factory.ErrorNode(_context, range, REF_ERROR.AsSpan());
                }

            // name_reference: BANG_NAME
            case Token.BANG_NAME:
                {
                    if (!TokenParser.TryParseBangName(_input.AsSpan(), _tokenSource, out var name))
                        throw Error($"A name can't be TRUE or FALSE, so '!{name}' is not a bang name.");

                    var start = _tokenSource.StartIndex;
                    Consume();
                    return _factory.BangName(_context, new SymbolRange(start, _tokenSource.StartIndex), name);
                }

            // external_cell_reference: SHEET_RANGE_PREFIX (A1_CELL | A1_CELL COLON A1_CELL | A1_SPAN_REFERENCE)
            case Token.SHEET_RANGE_PREFIX:
                {
                    var start = _tokenSource.StartIndex;
                    var prefix = SheetPrefix.ReadRange(_input.AsSpan(), _tokenSource);
                    Consume();

                    var area = A1Reference();
                    if (area is null)
                        throw UnexpectedTokenError(Token.A1_CELL, Token.A1_SPAN_REFERENCE);

                    var end = _tokenSource.StartIndex;
                    return prefix.BookIndex is not null
                        ? _factory.ExternalReference3D(_context, new SymbolRange(start, end), prefix.BookIndex.Value, prefix.FirstSheet!, prefix.LastSheet!, area.Value)
                        : _factory.Reference3D(_context, new SymbolRange(start, end), prefix.FirstSheet!, prefix.LastSheet!, area.Value);
                }

            // ref_function_call
            case Token.REF_FUNCTION_LIST:
                return LocalFunctionCall();

            // name_reference | structure_reference - all variants are expanded from the grammar.

            // Either defined name or table name for a structure reference
            case Token.NAME:
                {
                    var start = _tokenSource.StartIndex;
                    var localName = TokenParser.ParseName(_input.AsSpan(), _tokenSource);
                    Consume();
                    if (_la == Token.INTRA_TABLE_REFERENCE)
                    {
                        TokenParser.ParseIntraTableReference(_input.AsSpan(), _tokenSource, out var specifics, out var firstColumn, out var lastColumn);
                        Consume();
                        var range = new SymbolRange(start, _tokenSource.StartIndex);
                        return _factory.StructureReference(_context, range, localName, specifics, firstColumn, lastColumn ?? firstColumn);
                    }

                    // 3D reference
                    if (_la == Token.COLON && LL(1) == Token.SINGLE_SHEET_PREFIX)
                    {
                        var firstSheetName = localName;
                        Consume(); // COLON

                        // TODO: Decouple book prefix from single sheet prefix
                        var lastPrefix = SheetPrefix.ReadSingle(_input.AsSpan(), _tokenSource);
                        if (lastPrefix.BookIndex is not null)
                            throw Error("External workbook not expected.");

                        var lastSheetName = lastPrefix.FirstSheet!;

                        Consume(); // SINGLE_SHEET_PREFIX

                        // After prefix, there must be A1Reference
                        var area = A1Reference();
                        if (area is null)
                            throw UnexpectedTokenError(Token.A1_CELL, Token.A1_SPAN_REFERENCE);

                        var end = _tokenSource.StartIndex;
                        return _factory.Reference3D(_context, new SymbolRange(start, end), firstSheetName, lastSheetName, area.Value);
                    }

                    return _factory.Name(_context, new SymbolRange(start, _tokenSource.StartIndex), localName);
                }

            // reference to another workbook or to an item of a DDE link
            case Token.BOOK_PREFIX:
                {
                    var start = _tokenSource.StartIndex;
                    var bookPrefix = SheetPrefix.ReadBookPrefix(_input.AsSpan(), _tokenSource);
                    Consume();

                    // dde_reference: BOOK_PREFIX DDE_ITEM
                    if (_la == Token.DDE_ITEM)
                    {
                        var item = TokenParser.ParseDdeItem(_input.AsSpan(), _tokenSource);
                        Consume();
                        return _factory.ExternalDynamicDataExchange(_context, new SymbolRange(start, _tokenSource.StartIndex), bookPrefix, item);
                    }

                    var externalNameToken = _tokenSource;
                    Match(Token.NAME);
                    var externalName = TokenParser.ParseName(_input.AsSpan(), externalNameToken);
                    if (_la == Token.INTRA_TABLE_REFERENCE)
                    {
                        TokenParser.ParseIntraTableReference(_input.AsSpan(), _tokenSource, out var specifics, out var firstColumn, out var lastColumn);
                        Consume();
                        var range = new SymbolRange(start, _tokenSource.StartIndex);
                        return _factory.ExternalStructureReference(_context, range, bookPrefix, externalName, specifics, firstColumn, lastColumn ?? firstColumn);
                    }

                    return _factory.ExternalName(_context, new SymbolRange(start, _tokenSource.StartIndex), bookPrefix, externalName);
                }
            // name_reference: SINGLE_SHEET_PREFIX NAME
            // external_cell_reference: SINGLE_SHEET_PREFIX (A1_CELL | A1_CELL COLON A1_CELL | A1_SPAN_REFERENCE | REF_CONSTANT)
            // dde_reference: SINGLE_SHEET_PREFIX DDE_ITEM
            case Token.SINGLE_SHEET_PREFIX:
                {
                    var start = _tokenSource.StartIndex;
                    var sheetPrefix = _tokenSource;
                    var prefix = SheetPrefix.ReadSingle(_input.AsSpan(), sheetPrefix);
                    var sheetName = prefix.FirstSheet!;
                    Consume();

                    var area = A1Reference();
                    if (area is not null)
                    {
                        var end = _tokenSource.StartIndex;
                        return prefix.BookIndex is null
                            ? _factory.SheetReference(_context, new SymbolRange(start, end), sheetName, area.Value)
                            : _factory.ExternalSheetReference(_context, new SymbolRange(start, end), prefix.BookIndex.Value, sheetName, area.Value);
                    }

                    if (_la == Token.REF_CONSTANT)
                    {
                        var error = GetCurrentToken(); // Sheet1!#REF! is a valid
                        Consume();
                        return _factory.SheetErrorNode(_context, new SymbolRange(start, _tokenSource.StartIndex), prefix.BookIndex, sheetName, error);
                    }

                    // The prefix of a displayed DDE formula is the application and the topic of the link, e.g. `Sdemo123|tik!`.
                    // A sheet name can contain `|` too, but only a DDE link is followed by a quoted item.
                    if (_la == Token.DDE_ITEM)
                    {
                        if (!prefix.TryGetDdeLink(out var application, out var topic))
                            throw Error($"A dynamic data exchange item must follow a book prefix or an 'application|topic' prefix, but the prefix is '{_input.Substring(sheetPrefix.StartIndex, sheetPrefix.Length)}'.");

                        var item = TokenParser.ParseDdeItem(_input.AsSpan(), _tokenSource);
                        Consume();
                        return _factory.DynamicDataExchange(_context, new SymbolRange(start, _tokenSource.StartIndex), application, topic, item);
                    }

                    // name_reference
                    var nameToken = _tokenSource;
                    Match(Token.NAME);
                    var name = TokenParser.ParseName(_input.AsSpan(), nameToken);
                    var range = new SymbolRange(start, _tokenSource.StartIndex);
                    return prefix.BookIndex is null
                        ? _factory.SheetName(_context, range, sheetName, name)
                        : _factory.ExternalSheetName(_context, range, prefix.BookIndex.Value, sheetName, name);
                }

            // structure_reference - only for formulas directly in the table, e.g. totals row.
            case Token.INTRA_TABLE_REFERENCE:
                {
                    var start = _tokenSource.StartIndex;
                    TokenParser.ParseIntraTableReference(_input.AsSpan(), _tokenSource, out var specifics, out var firstColumn, out var lastColumn);
                    Consume();
                    var range = new SymbolRange(start, _tokenSource.StartIndex);
                    return _factory.StructureReference(_context, range, specifics, firstColumn, lastColumn ?? firstColumn);
                }
        }

        throw UnexpectedTokenError();
    }

    /// <summary>
    /// <code>
    /// a1_reference
    ///     : A1_CELL
    ///     | A1_CELL COLON A1_CELL
    ///     | A1_SPAN_REFERENCE
    ///     ;
    /// </code>
    /// </summary>
    private ReferenceArea? A1Reference()
    {
        var index = _tokenIndex;
        if (!TokenParser.TryReadReference(_style, _input.AsSpan(), _tokens, ref index, out var area))
            return null;

        // Continue at the token after the reference.
        _tokenIndex = index - 1;
        Consume();
        return area;
    }

    private TNode ErrorNode()
    {
        var start = _tokenSource.StartIndex;
        var errorToken = GetCurrentToken();
        Span<char> normalizedError = stackalloc char[errorToken.Length];
        errorToken.ToUpperInvariant(normalizedError);
        Consume();
        return _factory.ErrorNode(_context, new SymbolRange(start, _tokenSource.StartIndex), normalizedError);
    }

    private TNode Constant()
    {
        var start = _tokenSource.StartIndex;
        switch (_la)
        {
            case Token.NONREF_ERRORS:
                return ErrorNode();

            case Token.LOGICAL_CONSTANT:
                var logical = ConvertLogical();
                Consume();
                return _factory.LogicalNode(_context, new SymbolRange(start, _tokenSource.StartIndex), logical);

            case Token.NUMERICAL_CONSTANT:
                var number = ConvertNumber();
                Consume();
                return _factory.NumberNode(_context, new SymbolRange(start, _tokenSource.StartIndex), number);

            case Token.STRING_CONSTANT:
                var text = ConvertText();
                Consume();
                return _factory.TextNode(_context, new SymbolRange(start, _tokenSource.StartIndex), text);

            case Token.OPEN_CURLY:
                Consume();
                var arrayElements = ConstantListRows(out var rows, out var columns);
                Match(Token.CLOSE_CURLY);
                return _factory.ArrayNode(_context, new SymbolRange(start, _tokenSource.StartIndex), rows, columns, arrayElements);

            default:
                throw UnexpectedTokenError();
        }
    }

    private List<TScalarValue> ConstantListRows(out int rows, out int columns)
    {
        // First use list with doubling strategy
        var elements = new List<TScalarValue>();
        var rowSize = ConstantListRow(elements);
        var height = 1;
        while (_la == Token.SEMICOLON)
        {
            Consume();
            var nextRowSize = ConstantListRow(elements);
            if (nextRowSize != rowSize)
                throw Error("Rows of an array don't have same size.");

            height++;
        }

        rows = height;
        columns = rowSize;
        return elements;
    }

    private int ConstantListRow(List<TScalarValue> arrayElements)
    {
        var origSize = arrayElements.Count;
        var arrayElement = ArrayConstant();
        arrayElements.Add(arrayElement);
        while (_la == Token.COMMA)
        {
            Consume();
            var nextElement = ArrayConstant();
            arrayElements.Add(nextElement);
        }

        return arrayElements.Count - origSize;
    }

    private TScalarValue ArrayConstant()
    {
        TScalarValue value;
        SymbolRange symbolRange;
        var start = _tokenSource.StartIndex;
        switch (_la)
        {
            case Token.REF_CONSTANT:
            case Token.NONREF_ERRORS:
                // Convert to upper case on stack, because length of an error is limited to ~20
                var errorToken = GetCurrentToken();
                Span<char> normalizedError = stackalloc char[errorToken.Length];
                errorToken.ToUpperInvariant(normalizedError);
                Consume();
                symbolRange = new SymbolRange(start, _tokenSource.StartIndex);
                value = _factory.ErrorValue(_context, symbolRange, normalizedError);
                break;

            case Token.LOGICAL_CONSTANT:
                var logicalValue = GetTokenLogicalValue();
                Consume();
                symbolRange = new SymbolRange(start, _tokenSource.StartIndex);
                value = _factory.LogicalValue(_context, symbolRange, logicalValue);
                break;
            case Token.MINUS:
                Consume();
                if (_la != Token.NUMERICAL_CONSTANT)
                    throw UnexpectedTokenError(Token.NUMERICAL_CONSTANT);

                var negativeNumberValue = -ParseNumber(GetCurrentToken());
                Consume();
                symbolRange = new SymbolRange(start, _tokenSource.StartIndex);
                value = _factory.NumberValue(_context, symbolRange, negativeNumberValue);
                break;

            case Token.PLUS:
                Consume();
                if (_la != Token.NUMERICAL_CONSTANT)
                    throw UnexpectedTokenError(Token.NUMERICAL_CONSTANT);

                var positiveNumberValue = ParseNumber(GetCurrentToken());
                Consume();
                symbolRange = new SymbolRange(start, _tokenSource.StartIndex);
                value = _factory.NumberValue(_context, symbolRange, positiveNumberValue);
                break;

            case Token.NUMERICAL_CONSTANT:
                var numberValue = ParseNumber(GetCurrentToken());
                Consume();
                symbolRange = new SymbolRange(start, _tokenSource.StartIndex);
                value = _factory.NumberValue(_context, symbolRange, numberValue);
                break;

            case Token.STRING_CONSTANT:
                var token = GetCurrentToken();
                Span<char> buffer = stackalloc char[token.Length];
                Consume();
                symbolRange = new SymbolRange(start, _tokenSource.StartIndex);
                value = ConvertTextValue(token, out var slice, ref buffer)
                    ? _factory.TextValue(_context, symbolRange, slice.ToString())
                    : _factory.TextValue(_context, symbolRange, buffer.ToString());
                break;

            default:
                throw UnexpectedTokenError();
        }

        return value;
    }

    private IReadOnlyList<TNode> ArgumentList()
    {
        // A special case, there are no arguments
        if (_la == Token.CLOSE_BRACE)
        {
            Consume();
            return Array.Empty<TNode>();
        }

        var args = new List<TNode>();
        while (true)
        {
            // At the start of the loop, previous argument
            // should have been consumed with a comma.
            if (_la == Token.COMMA)
            {
                // If there is a comma, it means there are
                // two commas in a row and thus a blank argument.
                var start = _tokenSource.StartIndex;
                Consume();
                args.Add(_factory.BlankNode(_context, new SymbolRange(start, start)));
            }
            else if (_la == Token.CLOSE_BRACE)
            {
                // if there is a brace, it means the previous
                // comma is immediately followed by a brace `,)`
                // thus there is a blank node and end of args.
                var start = _tokenSource.StartIndex;
                Consume();
                args.Add(_factory.BlankNode(_context, new SymbolRange(start, start)));
                return args;
            }
            else
            {
                // Path for a non-blank argument.
                var arg = Expression(true, out _);
                args.Add(arg);
                if (_la == Token.CLOSE_BRACE)
                {
                    Consume();
                    return args;
                }

                // Each argument must be followed by a comma. 
                Match(Token.COMMA);
            }
        }
    }

    private void Match(int expected)
    {
        if (_la != expected)
            throw UnexpectedTokenError(expected);

        Consume();
    }

    private void Consume()
    {
        _tokenSource = _tokens[++_tokenIndex];
        _la = _tokenSource.SymbolId;
    }

    private int LL(int lookAhead)
    {
        var idx = _tokenIndex + lookAhead;
        return idx < _tokens.Count ? _tokens[idx].SymbolId : Token.EofSymbolId;
    }

    /// <summary>
    /// Does the token before the current one end with a space? A caller has read at least one token, so there
    /// always is a token before the current one.
    /// </summary>
    private bool IsSpaceAfterPreviousToken()
    {
        return TokenParser.IsSpaceAtEnd(_input.AsSpan(), _tokens[_tokenIndex - 1]);
    }

    /// <summary>
    /// Can the token start a <c>ref_atom_expression</c>, i.e. does <see cref="RefAtomExpression"/> accept it?
    /// </summary>
    private static bool StartsRefAtomExpression(int symbolId)
    {
        switch (symbolId)
        {
            case Token.REF_CONSTANT:
            case Token.OPEN_BRACE:
            case Token.A1_CELL:
            case Token.A1_SPAN_REFERENCE:
            case Token.BANG_REFERENCE:
            case Token.BANG_NAME:
            case Token.SHEET_RANGE_PREFIX:
            case Token.REF_FUNCTION_LIST:
            case Token.NAME:
            case Token.BOOK_PREFIX:
            case Token.SINGLE_SHEET_PREFIX:
            case Token.INTRA_TABLE_REFERENCE:
                return true;

            default:
                return false;
        }
    }

    private static double ParseNumber(ReadOnlySpan<char> number)
    {
        return double.Parse(
#if NETSTANDARD2_1
            number,
#else
            number.ToString(),
#endif
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture);
    }

    private bool ConvertLogical()
    {
        return GetTokenLogicalValue();
    }

    private bool GetTokenLogicalValue()
    {
        return _input[_tokenSource.StartIndex] is 'T' or 't';
    }

    private double ConvertNumber()
    {
        return ParseNumber(GetCurrentToken());
    }

    private string ConvertText()
    {
        var token = GetCurrentToken();
        Span<char> buffer = stackalloc char[token.Length];
        return ConvertTextValue(token, out var slice, ref buffer)
            ? slice.ToString()
            : buffer.ToString();
    }

    private static bool ConvertTextValue(ReadOnlySpan<char> token, out ReadOnlySpan<char> copy, ref Span<char> buffer)
    {
        var text = token.Slice(1, token.Length - 2);
        var indexOfDQuote = text.IndexOf('"');
        var textMustBeUnescaped = indexOfDQuote >= 0;
        if (!textMustBeUnescaped)
        {
            copy = text;
            return true;
        }

        Span<char> unescaped = buffer;
        var tail = unescaped;
        var quoteCount = 0;
        do
        {
            var quoteText = text.Slice(0, indexOfDQuote + 1);
            quoteText.CopyTo(tail);
            tail = tail.Slice(indexOfDQuote + 1);
            text = text.Slice(indexOfDQuote + 2);
            indexOfDQuote = text.IndexOf('"');
            quoteCount++;
        } while (indexOfDQuote >= 0);

        text.CopyTo(tail);
        buffer = unescaped.Slice(0, token.Length - 2 - quoteCount);
        copy = default;
        return false;
    }

    private TNode LocalFunctionCall()
    {
        var start = _tokenSource.StartIndex;
        var functionName = TokenParser.ParseFunctionName(_input.AsSpan(), _tokenSource);
        Consume();
        var args = ArgumentList();
        var range = new SymbolRange(start, _tokenSource.StartIndex);
        return _factory.Function(_context, range, functionName, args);
    }

    private Exception UnexpectedTokenError(params int[] expectedToken)
    {
        return Error($"Unexpected token {GetLaTokenName()}, expected one of {string.Join(",", expectedToken.Select(GetTokenName))}.");
    }

    private Exception UnexpectedTokenError()
    {
        return Error($"Unexpected token {GetLaTokenName()}.");
    }

    private Exception Error(string message)
    {
        return new ParsingException($"Error at char {_tokenSource.StartIndex} of '{_input}': {message}");
    }

    private ReadOnlySpan<char> GetCurrentToken()
    {
        return _input.AsSpan(_tokenSource.StartIndex, _tokenSource.Length);
    }

    private static string GetTokenName(int tokenType) => Token.GetSymbolName(tokenType);

    private string GetLaTokenName() => GetTokenName(_la);

    /// <summary>
    /// An atom the parser has already read, when an expression in braces turns out to be a reference
    /// expression and the parser backtracks. It carries the index the atom starts at, because the
    /// parser has read past the atom and can no longer tell where it began.
    /// </summary>
    private readonly struct ReadAtom
    {
        internal ReadAtom(TNode node, int start)
        {
            Node = node;
            Start = start;
        }

        internal TNode Node { get; }

        internal int Start { get; }
    }
}