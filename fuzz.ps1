<#
.SYNOPSIS
    Fuzz ClosedXML.Parser, or replay saved inputs through the same oracle.

.DESCRIPTION
    Publishes ClosedXML.Parser.Fuzz, instruments ClosedXML.Parser.dll and ClosedXML.Parser.Ast.dll
    with SharpFuzz, and runs libFuzzer over a corpus. Seeds come from the committed corpus under
    src/ClosedXML.Parser.Fuzz/corpus/<target>, so a fresh clone starts where the last person started.

    With -Replay, no fuzzing happens: the published harness runs over saved inputs and prints what
    each one did, grouped by exception type and originating library frame. That is the step between
    a crash artifact and a defect entry, and it deliberately uses the same oracle as fuzzing, so
    triage can never disagree with the run that produced the artifact.

.EXAMPLE
    ./fuzz.ps1 -Target formula-a1 -MaxTotalTime 600

.EXAMPLE
    ./fuzz.ps1 -Target modify -Replay temp/fuzz/artifacts

.EXAMPLE
    ./fuzz.ps1 -Target convert -MaxTotalTime 120 -- -minimize_crash=1 -runs=100000 temp/fuzz/artifacts/crash-abc
#>
[CmdletBinding()]
param(
    [ValidateSet('formula-a1', 'formula-r1c1', 'modify', 'convert', 'reference', 'all')]
    [string] $Target = 'formula-a1',

    [string] $LibFuzzer = (Join-Path $PSScriptRoot 'tools\libfuzzer-dotnet-windows.exe'),

    [string] $Corpus,

    # A file or directory of saved inputs to run through the oracle and report on. No fuzzing.
    [string] $Replay,

    # A crash artifact to shrink to the smallest input that still reproduces it. No fuzzing, and no
    # corpus: libFuzzer takes the artifact as its only positional argument, which is why this is a
    # parameter rather than something to pass through -LibFuzzerArgument.
    [string] $Minimize,

    [int] $Timeout = 10,

    [int] $MaxTotalTime = 0,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $LibFuzzerArgument
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$allTargets = @('formula-a1', 'formula-r1c1', 'modify', 'convert', 'reference')

if ($Target -eq 'all') {
    $failedTargets = [Collections.Generic.List[string]]::new()
    foreach ($targetName in $allTargets) {
        Write-Host "=== Fuzz target: $targetName ===" -ForegroundColor Cyan
        try {
            $childArguments = @{
                Target       = $targetName
                LibFuzzer    = $LibFuzzer
                Timeout      = $Timeout
                MaxTotalTime = $MaxTotalTime
            }
            if ($Corpus) { $childArguments.Corpus = $Corpus }
            if ($Replay) { $childArguments.Replay = $Replay }
            if ($LibFuzzerArgument) { $childArguments.LibFuzzerArgument = $LibFuzzerArgument }
            & $PSCommandPath @childArguments
            if ($LASTEXITCODE -ne 0) { throw "libFuzzer exited with code $LASTEXITCODE" }
        }
        catch {
            $failedTargets.Add($targetName)
            Write-Warning "Target '$targetName' stopped after a fuzzing failure: $($_.Exception.Message)"
        }
    }

    if ($failedTargets.Count -gt 0) {
        throw "Fuzzing completed with failures in: $($failedTargets -join ', ')"
    }
    return
}

function Resolve-ExistingPath([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not found: $Path"
    }
    return (Resolve-Path -LiteralPath $Path).Path
}

function Invoke-Native([string] $File, [string[]] $Arguments) {
    Write-Host "> $File $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $File"
    }
}

$repoRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$project = Join-Path $repoRoot 'src\ClosedXML.Parser.Fuzz\ClosedXML.Parser.Fuzz.csproj'
$workRoot = Join-Path $repoRoot 'temp\fuzz'
$publishRoot = Join-Path $workRoot 'publish'
$toolsRoot = Join-Path $workRoot 'tools'
$artifactRoot = Join-Path $workRoot 'artifacts'
$seedRoot = Join-Path $repoRoot (Join-Path 'src\ClosedXML.Parser.Fuzz\corpus' $Target)

$corpusPath = if ([string]::IsNullOrWhiteSpace($Corpus)) {
    Join-Path $workRoot (Join-Path 'corpus' $Target)
}
else {
    [IO.Path]::GetFullPath($Corpus, $repoRoot)
}

New-Item -ItemType Directory -Force -Path $toolsRoot, $corpusPath, $artifactRoot | Out-Null

# SharpFuzz rewrites the target assemblies in place. Always publish into a fresh, script-owned
# directory so a rerun never attempts to instrument an already instrumented assembly.
if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

Invoke-Native 'dotnet' @('publish', $project, '--configuration', 'Release', '--framework', 'net10.0', '--output', $publishRoot, '--no-self-contained')

$harness = Join-Path $publishRoot 'ClosedXML.Parser.Fuzz.exe'
$env:CLOSEDXML_FUZZ_TARGET = $Target

# Replay runs the harness directly. It must NOT be instrumented: SharpFuzz rewrites the assemblies
# to report coverage to a libFuzzer process that is not there.
if ($Replay) {
    $replayPath = [IO.Path]::GetFullPath($Replay, $repoRoot)
    if (-not (Test-Path -LiteralPath $replayPath)) {
        throw "Replay path was not found: $replayPath"
    }

    $env:CLOSEDXML_FUZZ_REPLAY = $replayPath
    try {
        & $harness
        $replayExit = $LASTEXITCODE
    }
    finally {
        Remove-Item Env:\CLOSEDXML_FUZZ_REPLAY -ErrorAction SilentlyContinue
    }

    exit $replayExit
}

if (-not (Test-Path -LiteralPath $LibFuzzer -PathType Leaf)) {
    # Say where the binary comes from rather than only that it is absent: everything else needed to
    # run a fuzzing session is in the repository, and this is the one remaining manual step.
    throw @"
libfuzzer-dotnet-windows.exe was not found at: $LibFuzzer

It is a prebuilt binary, cannot be restored by NuGet, and is gitignored. Download it from
https://github.com/Metalnem/libfuzzer-dotnet/releases and place it at tools\libfuzzer-dotnet-windows.exe,
or pass -LibFuzzer with its path.

Replay needs none of this: ./fuzz.ps1 -Target $Target -Replay <path> runs saved inputs through the
same oracle without libFuzzer.
"@
}

$libFuzzerPath = Resolve-ExistingPath $LibFuzzer 'libfuzzer-dotnet-windows.exe'

$tool = Join-Path $toolsRoot 'sharpfuzz.exe'
if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
    Invoke-Native 'dotnet' @('tool', 'install', 'SharpFuzz.CommandLine', '--tool-path', $toolsRoot, '--version', '2.3.0')
}

# Both assemblies, because the properties the targets check span the two: the parser reads a
# formula, the AST project writes it back out. Instrumenting only the parser would leave every
# display writer invisible to the coverage feedback.
Invoke-Native $tool @((Join-Path $publishRoot 'ClosedXML.Parser.dll'))
Invoke-Native $tool @((Join-Path $publishRoot 'ClosedXML.Parser.Ast.dll'))

# Seed from the committed corpus, so a fresh clone starts where the last person started.
$seedFiles = @(Get-ChildItem -LiteralPath $corpusPath -File -ErrorAction SilentlyContinue)
if ($seedFiles.Count -eq 0) {
    if (Test-Path -LiteralPath $seedRoot -PathType Container) {
        Write-Host "Seeding corpus from $seedRoot" -ForegroundColor DarkGray
        Copy-Item -Path (Join-Path $seedRoot '*') -Destination $corpusPath -Force
    }
    else {
        Write-Warning "No committed seed corpus at $seedRoot; starting from an empty corpus."
    }
}

# Tolerated-but-notable events are appended here rather than dropped. A run that finds no crash is
# not necessarily a run that found nothing.
$env:CLOSEDXML_FUZZ_REPORT_DIR = $artifactRoot

$artifactPrefix = $artifactRoot + [IO.Path]::DirectorySeparatorChar
$arguments = @("--target_path=$harness", "-timeout=$Timeout", "-artifact_prefix=$artifactPrefix")
if ($MaxTotalTime -gt 0) { $arguments += "-max_total_time=$MaxTotalTime" }

# Minimizing takes the artifact as libFuzzer's only positional argument, so it has to return before
# the corpus directory is appended below. The shrunk input is written to the artifact directory as
# minimized-from-<hash>.
if ($Minimize) {
    $minimizePath = Resolve-ExistingPath ([IO.Path]::GetFullPath($Minimize, $repoRoot)) 'Crash artifact to minimize'
    $arguments += @('-minimize_crash=1', '-runs=100000')
    if ($LibFuzzerArgument) { $arguments += $LibFuzzerArgument }
    $arguments += $minimizePath

    # A successful minimization ends with the crash still reproducing, so libFuzzer exits non-zero.
    # That is the expected outcome here and must not be reported as a failure.
    Write-Host "> $libFuzzerPath $($arguments -join ' ')" -ForegroundColor DarkGray
    & $libFuzzerPath @arguments
    return
}

# Every target decodes a short byte string into a formula, and real formulas are short. A larger
# max_len would only spend the budget on nesting depth, which the parser has no guard against and
# which says the same thing at 500 characters as at 50 000.
$arguments += '-max_len=512'
if ($LibFuzzerArgument) { $arguments += $LibFuzzerArgument }
$arguments += $corpusPath

if ($MaxTotalTime -le 0) {
    Invoke-Native $libFuzzerPath $arguments
    return
}

# Watchdog. If the harness fails during startup -- which it will if instrumented library code runs
# before Fuzzer.LibFuzzer.Run has allocated SharpFuzz's trace buffer -- libFuzzer notices only an
# exit code and then waits forever for a target that is already gone. It ignores its own
# -max_total_time in that state, so a dead harness looks from the outside exactly like a slow run.
$grace = 120
Write-Host "> $libFuzzerPath $($arguments -join ' ')" -ForegroundColor DarkGray
$process = Start-Process -FilePath $libFuzzerPath -ArgumentList $arguments -NoNewWindow -PassThru

if (-not $process.WaitForExit(($MaxTotalTime + $grace) * 1000)) {
    try { $process.Kill($true) } catch { Write-Warning "Could not kill libFuzzer: $($_.Exception.Message)" }
    throw @"
libFuzzer overran -max_total_time ($MaxTotalTime s) by more than $grace s and was killed.

The usual cause is the harness failing at startup rather than a slow run: libFuzzer reports that
only as an exit code and then waits indefinitely. Run the published harness in replay mode to see
the real exception:

  ./fuzz.ps1 -Target $Target -Replay src/ClosedXML.Parser.Fuzz/corpus/$Target
"@
}

if ($process.ExitCode -ne 0) {
    throw "libFuzzer exited with code $($process.ExitCode). A non-zero exit after a fuzzing run usually means a crash artifact was written to $artifactRoot; replay it with -Replay to see what it is."
}
