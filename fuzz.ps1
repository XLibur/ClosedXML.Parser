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

    With -Minimize, no fuzzing happens either: libFuzzer shrinks one crash artifact to the smallest
    input that still reproduces it.

.EXAMPLE
    ./fuzz.ps1 -Target formula-a1 -MaxTotalTime 600

.EXAMPLE
    ./fuzz.ps1 -Target modify -Replay temp/fuzz/artifacts

.EXAMPLE
    ./fuzz.ps1 -Target convert -Minimize temp/fuzz/artifacts/crash-abc
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

<#
.SYNOPSIS
    Everything one target does. Reports failure by throwing, never by exiting.

.DESCRIPTION
    A function rather than a second invocation of this script, because -Target all used to run the
    script again in the same process and an `exit` anywhere inside it took the parent down with it:
    a replay of five targets stopped after the first. Throwing is also what the -Target all loop
    already catches, so one target failing no longer hides the four behind it.

    Nothing here returns a value. Native commands write to stdout, and a function that returned an
    exit code would hand the caller libFuzzer's entire output along with it.
#>
function Invoke-FuzzTarget {
    param(
        [string] $Target,
        [string] $LibFuzzer,
        [string] $Corpus,
        [string] $Replay,
        [string] $Minimize,
        [int] $Timeout,
        [int] $MaxTotalTime,
        [string[]] $LibFuzzerArgument
    )

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

        # A replay reports; it does not pass judgement on the run. The harness exits non-zero only
        # when it could not read the inputs at all, which is worth stopping for.
        if ($replayExit -ne 0) {
            throw "Replay of '$replayPath' could not read its inputs (harness exit code $replayExit)."
        }

        return
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

    # Minimizing takes the artifact as libFuzzer's only positional argument, so it has to return
    # before the corpus directory is appended below. The shrunk input is written to the artifact
    # directory as minimized-from-<hash>.
    if ($Minimize) {
        $minimizePath = Resolve-ExistingPath ([IO.Path]::GetFullPath($Minimize, $repoRoot)) 'Crash artifact to minimize'
        $arguments += @('-minimize_crash=1', '-runs=100000')
        if ($LibFuzzerArgument) { $arguments += $LibFuzzerArgument }
        $arguments += $minimizePath

        $existing = [Collections.Generic.HashSet[string]]::new(
            [string[]] @(Get-ChildItem -LiteralPath $artifactRoot -Filter 'minimized-from-*' -File -ErrorAction SilentlyContinue | ForEach-Object Name),
            [StringComparer]::Ordinal)

        Write-Host "> $libFuzzerPath $($arguments -join ' ')" -ForegroundColor DarkGray
        & $libFuzzerPath @arguments

        # The exit code cannot tell success from failure here. A successful minimization ends with
        # the crash still reproducing, so libFuzzer exits non-zero -- and so does a driver that never
        # started, or an artifact that no longer reproduces at all. What separates them is whether a
        # smaller input was actually written, so that is what gets checked.
        $produced = @(Get-ChildItem -LiteralPath $artifactRoot -Filter 'minimized-from-*' -File -ErrorAction SilentlyContinue |
            Where-Object { -not $existing.Contains($_.Name) } | Sort-Object Length)

        if ($produced.Count -eq 0) {
            throw @"
Minimizing $minimizePath produced no smaller input (libFuzzer exit code $LASTEXITCODE).

Either the artifact no longer reproduces a failure -- replay it to see what it does now -- or
libFuzzer could not start the harness at all.

  ./fuzz.ps1 -Target $Target -Replay $minimizePath
"@
        }

        $smallest = $produced[0]
        Write-Host "Minimized to $($smallest.Length) bytes: $($smallest.FullName)" -ForegroundColor Green
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

    # ProcessStartInfo.ArgumentList rather than Start-Process -ArgumentList, which joins the array
    # into one command line with spaces and no quoting. Every path here is derived from the
    # repository root, so a clone under a directory with a space in its name would have split
    # --target_path and the corpus path into pieces and handed libFuzzer nonsense.
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $libFuzzerPath
    $startInfo.UseShellExecute = $false
    foreach ($argument in $arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::Start($startInfo)

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
}

# A crash artifact comes from one target and reproduces under that target's oracle, so there is no
# sensible way to minimize one against five. Said plainly rather than by quietly fuzzing instead,
# which is what happened while -Minimize went unforwarded to the children.
if ($Target -eq 'all' -and $Minimize) {
    throw "-Minimize takes a crash artifact, which belongs to one target. Name that target instead of 'all'."
}

if ($Target -ne 'all') {
    Invoke-FuzzTarget -Target $Target -LibFuzzer $LibFuzzer -Corpus $Corpus -Replay $Replay `
        -Minimize $Minimize -Timeout $Timeout -MaxTotalTime $MaxTotalTime -LibFuzzerArgument $LibFuzzerArgument
    return
}

$failedTargets = [Collections.Generic.List[string]]::new()
foreach ($targetName in $allTargets) {
    Write-Host "=== Fuzz target: $targetName ===" -ForegroundColor Cyan
    try {
        Invoke-FuzzTarget -Target $targetName -LibFuzzer $LibFuzzer -Corpus $Corpus -Replay $Replay `
            -Timeout $Timeout -MaxTotalTime $MaxTotalTime -LibFuzzerArgument $LibFuzzerArgument
    }
    catch {
        $failedTargets.Add($targetName)
        Write-Warning "Target '$targetName' stopped after a fuzzing failure: $($_.Exception.Message)"
    }
}

if ($failedTargets.Count -gt 0) {
    throw "Fuzzing completed with failures in: $($failedTargets -join ', ')"
}
