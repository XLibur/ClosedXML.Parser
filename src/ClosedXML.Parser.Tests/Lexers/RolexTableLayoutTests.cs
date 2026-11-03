using XLibur.Parser.Rolex;

namespace XLibur.Parser.Tests.Lexers;

/// <summary>
/// <c>RolexLexer.FindTransition</c> stops reading a transition's ranges at the first one whose lower bound
/// is above the character, so it is only right while Rolex emits the ranges sorted. Nothing else would say
/// so if a later Rolex build did not, and the short-input sweep would miss it for a rarely used character.
/// These walk the whole of both tables and check the layout that the lexer relies on.
/// </summary>
public class RolexTableLayoutTests
{
    public static TheoryData<string, int[]> Tables => new()
    {
        { "A1", RolexA1Dfa.DfaTable },
        { "R1C1", RolexR1C1Dfa.DfaTable },
    };

    [Theory]
    [MemberData(nameof(Tables))]
    public void Every_transition_lists_its_ranges_sorted_and_apart(string style, int[] table)
    {
        _ = style;
        foreach (var state in ReadStates(table))
        {
            foreach (var transition in state.Transitions)
            {
                var previousInTransition = -1;
                foreach (var (min, max) in transition.Ranges)
                {
                    Assert.True(min <= max, $"state {state.Offset}: range {min}-{max} is inverted");
                    Assert.True(min > previousInTransition, $"state {state.Offset}: range {min}-{max} is not after the one before it");
                    previousInTransition = max;
                }

                // A DFA state has at most one way out for a character, so no two transitions share a code point.
                foreach (var (min, max) in transition.Ranges)
                {
                    foreach (var other in state.Transitions)
                    {
                        if (ReferenceEquals(other, transition))
                            continue;
                        foreach (var (otherMin, otherMax) in other.Ranges)
                            Assert.False(min <= otherMax && otherMin <= max,
                                $"state {state.Offset}: {min}-{max} overlaps {otherMin}-{otherMax} of another transition");
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void Every_transition_leads_to_the_start_of_a_state(string style, int[] table)
    {
        _ = style;
        var states = ReadStates(table);
        var starts = states.Select(s => s.Offset).ToHashSet();

        Assert.Equal(0, states[0].Offset);
        foreach (var state in states)
        foreach (var transition in state.Transitions)
            Assert.True(starts.Contains(transition.Destination),
                $"state {state.Offset} leads to {transition.Destination}, which is not the start of a state");
    }

    private sealed record Transition(int Destination, List<(int Min, int Max)> Ranges);

    private sealed record State(int Offset, List<Transition> Transitions);

    private static List<State> ReadStates(int[] table)
    {
        var states = new List<State>();
        var pos = 0;
        while (pos < table.Length)
        {
            var offset = pos;
            var transitionCount = table[pos + 1];
            pos += 2;
            var transitions = new List<Transition>();
            for (var t = 0; t < transitionCount; t++)
            {
                var destination = table[pos];
                var rangeCount = table[pos + 1];
                pos += 2;
                var ranges = new List<(int, int)>();
                for (var r = 0; r < rangeCount; r++)
                {
                    ranges.Add((table[pos], table[pos + 1]));
                    pos += 2;
                }

                transitions.Add(new Transition(destination, ranges));
            }

            states.Add(new State(offset, transitions));
        }

        return states;
    }
}
