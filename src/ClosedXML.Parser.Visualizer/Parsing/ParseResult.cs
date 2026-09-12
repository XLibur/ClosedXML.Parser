using ClosedXML.Parser.Visualizer.Diagram;

namespace ClosedXML.Parser.Visualizer.Parsing;

/// <summary>
/// The outcome of parsing a formula: a <see cref="ParsedFormula"/> or a <see cref="FailedParse"/>.
/// </summary>
public abstract record ParseResult;

public sealed record ParsedFormula(DiagramModel Diagram) : ParseResult;

/// <param name="Message">The message of the parser.</param>
/// <param name="Position">
/// The position of the error in the formula, or null when the message has no position. It can
/// be the formula length, when the formula ended too soon.
/// </param>
public sealed record FailedParse(string Message, int? Position) : ParseResult;
