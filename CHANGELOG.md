# Changelog

All notable changes to XLibur.ClosedXML.Parser are recorded here. The Publish Release
workflow rolls the Unreleased section into a dated version heading, so add an entry
under Unreleased with each change.

## Contents

- [Unreleased](#unreleased)

## Unreleased

### Changed

- Forked from ClosedXML.Parser 2.0.0 and published as `XLibur.ClosedXML.Parser`. The
  `ClosedXML.Parser` namespace is unchanged.

### Fixed

- Parse a quoted sheet prefix in the Pratt parser. `QIdent` was lexed but no prefix
  parselet was registered for it, so every quoted sheet reference failed — `'New York'!A1`
  and `'Jane''s'!A1` as much as anything else a serializer quotes. The new parselet strips
  the apostrophes, collapses the doubled ones, and handles `'sheet'!A1`, `'sheet'!name` and
  `'first:last'!A1`. External workbook prefixes stay unsupported, as they are on the
  unquoted path.
- Write formula numbers with the invariant culture. `ToR1C1` used the current culture's
  negative sign, so under sv-SE, fi-FI or nb-NO (negative sign U+2212) it emitted
  `RC[−1]`, which the R1C1 reader could not parse back. Upstream issue
  [ClosedXML/ClosedXML.Parser#30](https://github.com/ClosedXML/ClosedXML.Parser/issues/30).
- Parse a keyword list that is a whole inner reference.
