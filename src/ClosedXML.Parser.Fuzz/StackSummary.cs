namespace ClosedXML.Parser.Fuzz;

/// <summary>
/// Reduces a stack trace to the one frame worth reading.
///
/// Shared by every part of the harness that describes a failure, because they must group inputs the
/// same way. When each has its own copy, one prefers the outermost frame and reports a BCL helper
/// for a defect that is in the lexer.
/// </summary>
internal static class StackSummary
{
    /// <summary>
    /// The outermost frame belonging to the library, falling back to the outermost frame of any
    /// kind.
    ///
    /// A precondition failure raised through a BCL helper puts that helper at the top of the stack,
    /// which identifies nothing: the question worth answering is which part of the parser asked for
    /// something impossible, not which utility class noticed.
    /// </summary>
    public static string FirstMeaningfulFrame(Exception exception)
    {
        var stack = exception.StackTrace;
        if (string.IsNullOrEmpty(stack))
            return "(no stack)";

        var frames = stack.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        if (frames.Length == 0)
            return "(no stack)";

        // The harness lives in ClosedXML.Parser.Fuzz, so a frame of its own would match a bare
        // "ClosedXML.Parser." test. Prefer a library frame, and only then anything at all.
        var library = Array.Find(frames, f =>
            f.Contains("ClosedXML.Parser.", StringComparison.Ordinal) &&
            !f.Contains("ClosedXML.Parser.Fuzz.", StringComparison.Ordinal));

        return library ?? frames[0];
    }

    /// <summary>
    /// A grouping key for one failure: the exception type and where it came from.
    ///
    /// Deliberately excludes the message, which carries input-derived text — a position, a token
    /// name, the formula itself — and would split one defect into as many groups as there are
    /// inputs that reach it.
    /// </summary>
    public static string Signature(Exception exception)
    {
        // Group by the underlying fault, but keep the wrapper's identity. A property violation
        // wrapping a ParsingException and the same ParsingException thrown on the way in are
        // different findings with identical frames.
        return exception.InnerException is null
            ? $"{exception.GetType().FullName} at {FirstMeaningfulFrame(exception)}"
            : $"{exception.GetType().Name} -> {exception.InnerException.GetType().FullName} at {FirstMeaningfulFrame(exception.InnerException)}";
    }
}
