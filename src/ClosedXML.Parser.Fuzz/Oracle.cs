namespace ClosedXML.Parser.Fuzz;

/// <summary>
/// Decides whether what a target just did was acceptable.
///
/// <para>
/// The rule that shapes this file is that <em>the phase matters</em>. Handing the parser text that
/// is not a formula and having it refuse is correct behaviour. Handing it text it already parsed
/// and having it fail to write that formula back out is not: by the time a display string is
/// written, the parser has claimed to understand the input. A single flat allowlist cannot express
/// that difference, and would file write-path defects as malformed input.
/// </para>
///
/// <para>
/// <b>No rule here may inspect <see cref="Exception.Message"/>.</b> A message is prose belonging to
/// another assembly; it changes without notice, and when it does the branch stops firing silently
/// and a case the harness meant to ignore is reported as a crash on every run. Where a rejection
/// cannot be recognised by its type, the fix is to give it a type in the library.
/// </para>
/// </summary>
internal static class Oracle
{
    /// <summary>
    /// The one exception type that means "this text is not a formula", raised while parsing.
    ///
    /// <para>
    /// <see cref="ParsingException"/> is the library's own statement of refusal, and every
    /// deliberate refusal in the lexer and the parser uses it: an unpaired surrogate, an
    /// unterminated literal, a partially matched token, a token the lexer cannot start, a trailing
    /// remainder. Nothing else on the parse path is a considered refusal.
    /// </para>
    ///
    /// <para>
    /// <b><see cref="ArgumentException"/> is deliberately absent</b>, and so is
    /// <see cref="InvalidOperationException"/>. An argument exception names a parameter of a method
    /// the caller never invoked, so it can only be an internal precondition escaping. The library
    /// raises <c>InvalidOperationException("Bug in token parser. Token doesn't have expected
    /// format.")</c> in as many words — tolerating that type would discard the finding it exists to
    /// announce. <see cref="NotSupportedException"/> is absent for the same reason: it guards the
    /// default arm of the reference-style switches, which no input should reach.
    /// </para>
    /// </summary>
    public static bool IsRejectionDuringParse(Exception exception)
    {
        return exception is ParsingException;
    }

    /// <summary>
    /// Tolerated once the parser has accepted the input — while writing a display string, modifying
    /// a formula or converting between reference styles.
    ///
    /// <para>
    /// Deliberately almost empty. A formula can describe an array literal of many thousand elements
    /// or a string the writer expands, so exhausting memory is a resource result rather than a
    /// defect — but see <see cref="ShouldReport"/>: tolerating it does not mean ignoring it.
    /// </para>
    /// </summary>
    public static bool IsToleratedDuringProcess(Exception exception)
    {
        return exception is OutOfMemoryException;
    }

    /// <summary>
    /// Nothing is tolerated when re-reading text the library itself just wrote. If the library
    /// produced that formula then the library can parse it; anything else is silent corruption in
    /// the write path, which is the failure no exception check on the first parse can ever see.
    /// </summary>
    public static bool IsToleratedDuringReparse(Exception exception)
    {
        _ = exception;
        return false;
    }

    /// <summary>
    /// Whether a tolerated exception is nonetheless worth putting in front of a human. A run that
    /// finds no crash is not necessarily a run that found nothing.
    /// </summary>
    public static bool ShouldReport(Exception exception)
    {
        return exception is OutOfMemoryException or NotImplementedException;
    }

    /// <summary>
    /// Messages already written, so a gap the fuzzer reaches a million times is recorded once.
    /// </summary>
    /// <remarks>
    /// Keyed on the message rather than the type, because one type routinely covers several
    /// distinct gaps. The message of a <see cref="NotSupportedException"/> from a display writer
    /// says which switch fell through; the type alone would collapse the inventory to one line.
    /// </remarks>
    private static readonly HashSet<string> Reported = [];

    /// <summary>Write a tolerated-but-notable event where a human will find it after the run.</summary>
    public static void Report(string target, string phase, Exception exception)
    {
        // A run executes millions of inputs and rediscovers the same gap constantly. Without this,
        // tolerated.tsv becomes a multi-gigabyte file saying one thing.
        if (!Reported.Add($"{exception.GetType().FullName}\t{exception.Message}"))
            return;

        // Console output is unreliable under a fuzzing driver, so this goes to a file. The
        // directory comes from fuzz.ps1; without one the harness is being run by hand and the
        // console is fine.
        var line = $"{DateTime.UtcNow:O}\t{target}\t{phase}\t{exception.GetType().FullName}\t{exception.Message}";
        var directory = Environment.GetEnvironmentVariable("CLOSEDXML_FUZZ_REPORT_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.Error.WriteLine(line);
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "tolerated.tsv"), line + Environment.NewLine);
        }
        catch (Exception e) when (IsDiagnosticsFailure(e))
        {
            // Reporting must never be the reason a run fails.
        }
    }

    /// <summary>
    /// Whether an exception from writing a diagnostics file may be swallowed.
    /// </summary>
    /// <remarks>
    /// Catching only <see cref="IOException"/> here is the obvious mistake and the wrong one. A
    /// report directory that is unwritable, on a read-only volume, or spelled with characters the
    /// platform rejects raises <see cref="UnauthorizedAccessException"/>,
    /// <see cref="NotSupportedException"/> or <see cref="ArgumentException"/> — none of which
    /// derive from <see cref="IOException"/>, so each would escape and <em>replace the finding it
    /// was recording</em>, reporting a misconfigured environment variable as a defect in the parser.
    /// <para>
    /// Deliberately a list rather than a bare <c>catch</c>: a <see cref="NullReferenceException"/>
    /// from this code is a bug in the harness and should still stop the run.
    /// </para>
    /// </remarks>
    public static bool IsDiagnosticsFailure(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException;
    }
}
