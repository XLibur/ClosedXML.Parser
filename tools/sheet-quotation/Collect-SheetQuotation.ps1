param(
    [Parameter(Mandatory)][string]$cpFile,
    [Parameter(Mandatory)][string]$out,
    [Parameter(Mandatory)][string]$namesOut,
    # first: <char>tag. mid: tag<char>zz. last: tag<char>.
    # `mid` and `last` both describe a non-first character and both feed the "next"
    # table, so collect both and union the results - the end of a name is a position
    # Excel can treat specially, and neither mode alone is the whole answer.
    [ValidateSet('first','mid','last')][string]$mode = 'mid'
)
$ErrorActionPreference = 'Stop'

# Sheet names must be unique and must not look like a cell reference, or Excel quotes
# them for that reason instead of for the character under test. The 5-letter tag is past
# the 3-letter column limit, so no name here can be mistaken for a reference.
function Enc([int]$n) { $s=''; for ($k=0;$k -lt 4;$k++) { $s = [char](97 + ($n % 26)) + $s; $n = [int][math]::Floor($n/26) }; return $s }

# The sheet name is the character under test surrounded by tag letters, so it can hold a
# comma, a quote, anything. Emit rows as objects and let ConvertTo-Csv quote them, rather
# than pasting fields together and corrupting the mapping for those very codepoints.
function New-ProbeRow([int]$cp, [string]$status, $probeRow, [string]$sheetName) {
    return [PSCustomObject]@{
        Codepoint = '{0:X4}' -f $cp
        Status    = $status
        Row       = $probeRow
        SheetName = $sheetName
    }
}

# Excel cannot be asked about these. Renaming a sheet to a name holding U+0003 opens a
# modal dialog that DisplayAlerts = $false does not suppress, and the COM session wedges
# until Excel is killed - so a full-BMP run has to skip them rather than discover them.
# A lone surrogate cannot round-trip through a sheet name at all. Neither can reach a
# file, both being invalid XML, so they keep whatever the data files already say.
function Test-Unprobeable([int]$cp) {
    return ($cp -le 0x1F) -or ($cp -ge 0xD800 -and $cp -le 0xDFFF)
}

$cps = [int[]](Get-Content $cpFile | Where-Object { $_.Trim() } | ForEach-Object { [int]("0x$_") })

# Workbook.SaveAs resolves a relative path against Excel's working directory, not this
# session's, so a relative -out would be deleted here and written somewhere else - and the
# harvest step would then read a stale workbook and record the wrong answers, silently.
$out = [IO.Path]::GetFullPath([IO.Path]::Combine((Get-Location).Path, $out))
$namesOut = [IO.Path]::GetFullPath([IO.Path]::Combine((Get-Location).Path, $namesOut))
if (Test-Path $out) { Remove-Item $out -Force }

$xl = New-Object -ComObject Excel.Application
$xl.Visible = $false; $xl.DisplayAlerts = $false; $xl.ScreenUpdating = $false
$rows = New-Object System.Collections.Generic.List[object]
try {
    $wb = $xl.Workbooks.Add()
    $probe = $wb.Worksheets.Item(1)
    $probe.Name = 'Probe'
    $row = 0
    for ($i = 0; $i -lt $cps.Count; $i++) {
        $cp = $cps[$i]
        if (Test-Unprobeable $cp) {
            $rows.Add((New-ProbeRow $cp 'UNPROBEABLE' '' ''))
            continue
        }

        $ch = [char]$cp; $tag = 'q' + (Enc $i)
        $name = switch ($mode) {
            'first' { "$ch$tag" }
            'mid'   { "$tag$ch" + 'zz' }
            'last'  { "$tag$ch" }
        }
        $ws = $null
        try { $ws = $wb.Worksheets.Add(); $ws.Name = $name }
        catch { $rows.Add((New-ProbeRow $cp 'INVALID' '' '')); continue }
        $row++
        # U+0027 is legal mid-name, so the reference written here has to escape it, or
        # the probe formula is malformed and Excel rejects the assignment.
        $ref = $name.Replace("'", "''")
        try { $probe.Range("A$row").Formula = "='$ref'!A1" }
        catch { $row--; $rows.Add((New-ProbeRow $cp 'NOFORMULA' '' '')); continue }
        $rows.Add((New-ProbeRow $cp 'OK' $row $name))
    }
    $wb.SaveAs($out, 51)
    $wb.Close($false)
} finally {
    $xl.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
}
$csv = $rows | ConvertTo-Csv -NoTypeInformation
[IO.File]::WriteAllLines($namesOut, $csv, [Text.UTF8Encoding]::new($false))
"built $out ($($rows.Count) entries, $row formulas)"
