using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace XLibur.Parser.Rolex;

/// <summary>
/// A class required by a Rolex tool. Never used.
/// </summary>
internal class TableTokenizer
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Parameters must be present to satisfy machine generated contract.")]
    protected TableTokenizer(int[] dfaTable, int[][] blockEnds, int[] nodeFlags, IEnumerable<char> input)
    {
        throw new NotSupportedException("This class should never be instantiated.");
    }
}

/// <summary>
/// A class required by a Rolex tool: the generated table has a constructor that takes a
/// <see cref="TextReader"/>. Never used.
/// </summary>
internal class TextReaderEnumerable : IEnumerable<char>
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Parameters must be present to satisfy machine generated contract.")]
    public TextReaderEnumerable(TextReader input)
    {
        throw new NotSupportedException("This class should never be instantiated.");
    }

    public IEnumerator<char> GetEnumerator() => throw new NotSupportedException("This class should never be instantiated.");

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}