# Sheet quotation data

`ident-sheet-first.txt` and `ident-sheet-next.txt` in this folder record, for
every BMP codepoint, whether a sheet name containing that codepoint has to be quoted in
a formula. `NameUtils.QuoteFirst` and `NameUtils.QuoteNext` are compiled copies of those
two files. These scripts are how the data is collected.

## Why the data comes from a saved file

Excel's formula bar and Excel's file format do not agree. For a sheet named `ABC～`
(U+FF5E) the formula bar shows

    =ABC～!A1

but the same workbook stores

    <f>'ABC～'!A1</f>

A table collected from the formula bar therefore marks characters as needing no quotes
when Excel itself quotes them, and for some of those characters Excel refuses to open a
file that references them unquoted. Which ones depends on the position: U+2028, U+2029,
U+202A–U+202E, U+303D and U+303E as the first character of a name, U+2065–U+2069 and
U+303D anywhere in it. That is why the data is read back out of a saved `.xlsx` rather
than from the UI, and why the two positions get separate tables.

A codepoint Excel refuses in a sheet name has no row at all, rather than a row recording
whatever the probe fell back to. `* / : ? [ \ ]` are refused anywhere and are in neither
file. An apostrophe is refused only as the first character, so `ident-sheet-first.txt`
has no `0027` row while `ident-sheet-next.txt` keeps its `0027 YES`, which is a real
observation. `NameUtils.ShouldQuote` answers for a leading apostrophe before it reaches
the mask: it is written quoted, which is what the application or the topic of a DDE link
needs, and no sheet name may start with one.

## Running a collection

Needs Excel installed and PowerShell. Excel is driven over COM, invisibly.

    # one batch of codepoints, one workbook - once per mode
    ./Collect-SheetQuotation.ps1 -cpFile batch.txt -out batch.xlsx -namesOut batch.csv -mode mid
    ./Collect-SheetQuotation.ps1 -cpFile batch.txt -out batch-last.xlsx -namesOut batch-last.csv -mode last

`-cpFile` is one 4-digit hex codepoint per line. `-mode first` puts the character first,
`-mode mid` puts it between two letter runs, `-mode last` puts it at the end. The script
names one sheet per codepoint, references each from a `Probe` sheet, and saves. Read the
stored `<f>` for each probe cell out of the saved XML: a leading apostrophe means the
codepoint needs quotes.

`first` feeds `ident-sheet-first.txt`. `mid` and `last` both describe a non-first
character, so both feed `ident-sheet-next.txt` and **their results must be unioned** —
the end of a name is a position Excel can treat specially, so running only one of them
leaves half the answer uncollected.

`-namesOut` is the mapping from codepoint to probe row, written as CSV with a header
(`Codepoint,Status,Row,SheetName`). Read it with a real CSV parser: a sheet name is the
character under test surrounded by tag letters, so it can contain a comma or a quote.
`Status` is `OK` (row holds a formula), `INVALID` (Excel refused the name), `NOFORMULA`
(Excel refused the reference) or `UNPROBEABLE` (skipped, see below).

Batch it at about 1000 codepoints per workbook and run each batch with a timeout —
around 45 seconds each, so a full BMP pass is roughly an hour per mode.

The committed tables were collected with `first` and `last`, then checked with a full
`mid` pass over the BMP: `mid` produced no codepoint needing quotes that `last` had not
already caught, so the two non-first positions agree.

## Things that will bite you

- **U+0000–U+001F cannot be probed.** Renaming a sheet to a name holding U+0003 opens a
  modal dialog that `DisplayAlerts = $false` does not suppress, and the COM session
  wedges until Excel is killed. The MS-XLSX grammar singles out `END OF TEXT` as
  forbidden. The script skips these rather than discovering them the hard way, and
  records them as `UNPROBEABLE`; they keep their previous values.
- **Lone surrogates cannot be probed** either. Skipped and recorded the same way.
- **Sheet names must not look like a cell reference.** Excel quotes `S500` because it
  reads as one, not because of any character in it. The script's 5-letter tag is longer
  than the 3-letter column limit, which rules that out.
- **U+0027 can only be probed with `-mode mid`.** Excel forbids a sheet name that begins
  or ends with an apostrophe. Mid-name it is fine, as long as the probe formula doubles
  it — otherwise the reference is malformed and Excel rejects the assignment.
- **Excel's answers depend on its Unicode tables.** A newer build accepts bare
  codepoints that an older one quotes, because they were unassigned when the older build
  shipped. Merge a new collection into the existing data as a union — quote if either
  side says quote — so the output stays loadable on older Excel too.
