using System.Runtime.CompilerServices;
using SharpFuzz;

namespace ClosedXML.Parser.Fuzz;

/// <summary>
/// Entry point for the fuzzing harness. Two modes:
///
/// <list type="bullet">
/// <item><b>Fuzz</b> — the default. libFuzzer drives the process through SharpFuzz.</item>
/// <item><b>Replay</b> — set <c>CLOSEDXML_FUZZ_REPLAY</c> to a file or directory and the harness
/// runs the same target over those inputs, printing what each one did. This is triage, and it runs
/// the <em>same</em> oracle as fuzzing on purpose: a triage tool that judges differently from the
/// fuzzer is a tool that disagrees with its own findings.</item>
/// </list>
/// </summary>
internal static class Program
{
    private const string TargetEnvironmentVariable = "CLOSEDXML_FUZZ_TARGET";
    private const string ReplayEnvironmentVariable = "CLOSEDXML_FUZZ_REPLAY";

    public static int Main()
    {
        var target = Environment.GetEnvironmentVariable(TargetEnvironmentVariable) ?? FuzzTargets.Default;
        if (!FuzzTargets.IsKnown(target))
        {
            Console.Error.WriteLine(
                $"{TargetEnvironmentVariable} must be one of: {string.Join(", ", FuzzTargets.All)}.");
            return 2;
        }

        var replayPath = Environment.GetEnvironmentVariable(ReplayEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(replayPath)
            ? Replay(target, replayPath)
            : Fuzz(target);
    }

    /// <summary>
    /// Run the target under libFuzzer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No code that runs before <see cref="Fuzzer.LibFuzzer"/> starts may touch
    /// <c>ClosedXML.Parser</c> or <c>ClosedXML.Parser.Ast</c>.</b> SharpFuzz rewrites both to report
    /// coverage into a trace buffer that <c>Fuzzer.LibFuzzer.Run</c> is what allocates; rewritten
    /// code running earlier dereferences a buffer that is not there and the process dies during
    /// startup with a <c>TypeInitializationException</c> wrapping a <c>NullReferenceException</c>.
    /// </para>
    /// <para>
    /// A <em>reference</em> is enough — the branch need never execute, because JIT-compiling a
    /// method that mentions a type can load and initialise it. Hence the split into separate
    /// methods and hence <c>NoInlining</c>: if this body were inlined back into <c>Main</c>, the
    /// reference would return with it. The matching constraint on the harness side is in
    /// <see cref="FuzzTargets"/>, where the parser context, the AST factory and the identity
    /// modifier are all built on first use rather than in a static initialiser.
    /// </para>
    /// <para>
    /// Failure is near-silent: libFuzzer sees only an exit code and then waits for a target that is
    /// already gone, ignoring its own <c>-max_total_time</c>. Replay cannot catch it either,
    /// because replay runs an uninstrumented build. <c>fuzz.ps1</c>'s watchdog exists for this.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Fuzz(string target)
    {
        Fuzzer.LibFuzzer.Run(data => FuzzTargets.Run(target, data));
        return 0;
    }

    /// <summary>
    /// Run the target over saved inputs and report one line per input, plus a summary grouping them
    /// by failure. The grouping is the point: six crash artifacts that are all one bug should read
    /// as one bug.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Replay(string target, string path)
    {
        var files = Directory.Exists(path)
            ? Directory.GetFiles(path).OrderBy(f => f, StringComparer.Ordinal).ToArray()
            : [path];

        if (files.Length == 0)
        {
            Console.Error.WriteLine($"No inputs found at {path}.");
            return 2;
        }

        var distinct = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var bytes = File.ReadAllBytes(file);

            string signature;
            try
            {
                var outcome = FuzzTargets.Run(target, bytes);
                signature = $"(no failure: {outcome})";
                Console.WriteLine($"{name}\t{bytes.Length} bytes\t{outcome}");
            }
            catch (Exception exception)
            {
                signature = StackSummary.Signature(exception);
                Console.WriteLine($"{name}\t{bytes.Length} bytes\t{exception.GetType().FullName}\t{exception.Message}");
                Console.WriteLine($"    {StackSummary.FirstMeaningfulFrame(exception)}");
            }

            if (!distinct.TryGetValue(signature, out var members))
                distinct[signature] = members = [];

            members.Add(name);
        }

        Console.WriteLine();
        Console.WriteLine($"{files.Length} input(s), {distinct.Count} distinct outcome(s):");
        foreach (var (signature, members) in distinct.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            Console.WriteLine($"  {signature}  x{members.Count}");

        // A replay reports; it does not pass judgement on the run as a whole. Exit 0 unless the
        // inputs could not be read, so that a triage sweep is never mistaken for a failed build.
        return 0;
    }
}
