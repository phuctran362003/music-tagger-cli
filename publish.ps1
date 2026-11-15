<#
.SYNOPSIS
    Publishes MusicTagger CLI as a single Windows .exe file.

.DESCRIPTION
    Produces exactly one self-contained executable (or a tiny framework-dependent one)
    in the output folder, so the app can be copied anywhere and started by double-click.

    self-contained      ~72 MB, bundles the .NET 10 runtime, runs on a machine with no .NET installed.
    framework-dependent ~2 MB, needs the .NET 10 runtime on the target machine.

.EXAMPLE
    .\publish.ps1
    Builds publish\win-x64-self-contained\MusicTagger.Cli.exe (recommended).

.EXAMPLE
    .\publish.ps1 -Mode framework-dependent
    Builds publish\win-x64-fx\MusicTagger.Cli.exe (small, needs .NET 10 runtime).

.EXAMPLE
    .\publish.ps1 -Runtime win-arm64 -Version 1.2.0 -Output C:\apps\musictagger
#>
[CmdletBinding()]
param(
    [ValidateSet('self-contained', 'framework-dependent')]
    [string]$Mode = 'self-contained',

    [ValidateSet('win-x64', 'win-arm64', 'win-x86')]
    [string]$Runtime = 'win-x64',

    [string]$Version = '',

    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $repoRoot 'src\MusicTagger.Cli\MusicTagger.Cli.csproj'

if (-not (Test-Path $project)) {
    throw "The CLI project was not found at '$project'. Run this script from the repository."
}

$selfContained = ($Mode -eq 'self-contained')
$suffix = if ($selfContained) { 'self-contained' } else { 'fx' }

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $repoRoot (Join-Path 'publish' "$Runtime-$suffix")
}

$publishArgs = @(
    'publish', $project,
    '-c', 'Release',
    '-r', $Runtime,
    '--self-contained', $selfContained.ToString().ToLowerInvariant(),
    '-p:PublishSingleFile=true',
    '-p:DebugType=none',
    '-o', $Output
)

if ($selfContained) {
    # Without this the bundle leaves the native runtime libraries next to the .exe.
    $publishArgs += '-p:IncludeNativeLibrariesForSelfExtract=true'
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $publishArgs += "-p:Version=$Version"
}

Write-Host "Publishing $Mode ($Runtime) ..." -ForegroundColor Cyan
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $Output 'MusicTagger.Cli.exe'
if (-not (Test-Path $exe)) {
    throw "Publish finished but '$exe' does not exist."
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 2)
Write-Host ''
Write-Host "Done: $exe ($sizeMb MB)" -ForegroundColor Green
Write-Host "Smoke test:"
& $exe --version
if ($LASTEXITCODE -ne 0) {
    throw "The published executable did not start (exit code $LASTEXITCODE)."
}

Write-Host ''
Write-Host 'Settings, logs and backups are stored in %APPDATA%\MusicTagger, not next to the .exe.'
