using Antlr2Rolex;

const string usage = "Usage: Antlr2Rolex <lexer.g4> --style A1|R1C1 [--output <lexer.rl>]";

string? input = null;
string? output = null;
LexerStyle? style = null;
for (var i = 0; i < args.Length; ++i)
{
    var arg = args[i];
    var value = i + 1 < args.Length ? args[i + 1] : null;
    if (arg == "--style" && value is not null)
    {
        style = value.ToUpperInvariant() switch
        {
            "A1" => LexerStyle.A1,
            "R1C1" => LexerStyle.R1C1,
            _ => null,
        };
        ++i;
    }
    else if (arg == "--output" && value is not null)
    {
        output = value;
        ++i;
    }
    else if (input is null && !arg.StartsWith("--", StringComparison.Ordinal))
    {
        input = arg;
    }
    else
    {
        input = null;
        break;
    }
}

if (input is null || style is null)
{
    Console.Error.WriteLine(usage);
    return 2;
}

ConversionResult result;
try
{
    result = RolexGrammarConverter.Convert(File.ReadAllText(input), style.Value);
}
catch (AntlrGrammarException ex)
{
    Console.Error.WriteLine($"{input}: {ex.Message}");
    return 1;
}

foreach (var warning in result.Warnings)
    Console.Error.WriteLine($"warning: {warning}");

// The committed grammars are UTF-8 without a BOM and stored with LF line ends, which is what
// the converter produces and what File.WriteAllText writes.
if (output is null)
    Console.Out.Write(result.RolexGrammar);
else
    File.WriteAllText(output, result.RolexGrammar);

return 0;
