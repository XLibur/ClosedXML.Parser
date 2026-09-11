#!/usr/bin/env bash
# Regenerates the Rolex DFA tables (src/ClosedXML.Parser/Rolex/Rolex*Dfa.cs) from the Rolex
# grammars (Lexer*.rl) with the vendored Rolex 91a2d6d build, the only build whose output
# matches the tables. It is a .NET Framework executable, so run this on Windows (Git Bash).
#
#   tools/rolex/generate-dfa-tables.sh           write both tables
#   tools/rolex/generate-dfa-tables.sh --check   change nothing, fail if a committed table differs
#
# Line ends are not compared: Rolex writes CRLF and git stores the tables with LF.
set -euo pipefail

mode=write
case "${1:-}" in
    "") ;;
    --check) mode=check ;;
    *) echo "usage: $0 [--check]" >&2; exit 2 ;;
esac

root=$(cd "$(dirname "$0")/../.." && pwd)
rolex="$root/tools/rolex/91a2d6d/rolex.exe"
tables="$root/src/ClosedXML.Parser/Rolex"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# Git Bash rewrites an argument such as /noshared into a Windows path unless told not to.
export MSYS_NO_PATHCONV=1

status=0
for style in A1 R1C1; do
    cp "$tables/Lexer$style.rl" "$work/"
    # Rolex names the class after the output file, so the file name is the class name.
    (cd "$work" && "$rolex" "Lexer$style.rl" /noshared /output "Rolex${style}Dfa.cs" /namespace ClosedXML.Parser.Rolex > /dev/null)

    generated="$work/Rolex${style}Dfa.cs"
    committed="$tables/Rolex${style}Dfa.cs"
    if [ "$mode" = write ]; then
        tr -d '\r' < "$generated" > "$committed"
        echo "wrote Rolex${style}Dfa.cs from Lexer$style.rl"
    elif cmp -s <(tr -d '\r' < "$generated") <(tr -d '\r' < "$committed"); then
        echo "Rolex${style}Dfa.cs is generated from Lexer$style.rl"
    else
        echo "Rolex${style}Dfa.cs is not generated from Lexer$style.rl. Run tools/rolex/generate-dfa-tables.sh and commit the tables." >&2
        diff <(tr -d '\r' < "$committed") <(tr -d '\r' < "$generated") | head -n 20 >&2 || true
        status=1
    fi
done

exit $status
