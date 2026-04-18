param(
    [string]$DalamudLibPath = "C:\Users\lokin\AppData\Roaming\XIVLauncher\addon\Hooks\dev",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "",
    [string]$Version = "",
    [switch]$UseLocalBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

if ([string]::IsNullOrWhiteSpace($OutputRoot))
{
    $OutputRoot = Join-Path $repoRoot ".plogon-local-build"
}

$outputDir = Join-Path $OutputRoot "output"

if (-not (Test-Path -LiteralPath $DalamudLibPath))
{
    throw "DalamudLibPath not found: $DalamudLibPath"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$projectPathInContainer = "/work/repo/Echoglossian.csproj"
$dalamudPathInContainer = "/work/dalamud/"

$msbuildArgs = @(
    "build",
    $projectPathInContainer,
    "-c", $Configuration,
    "-o", "/output",
    "-p:DalamudLibPath=$dalamudPathInContainer",
    "-p:IsPlogonBuild=True"
)

if (-not [string]::IsNullOrWhiteSpace($Version))
{
    $msbuildArgs += "-p:Version=$Version"
}

if ($UseLocalBuild)
{
    $localArgs = @(
        "build",
        (Join-Path $repoRoot "Echoglossian.csproj"),
        "-c", $Configuration,
        "-o", $outputDir,
        "-p:DalamudLibPath=$DalamudLibPath",
        "-p:IsPlogonBuild=True"
    )

    if (-not [string]::IsNullOrWhiteSpace($Version))
    {
        $localArgs += "-p:Version=$Version"
    }

    Write-Host "Running local fallback build..."
    Write-Host "dotnet $($localArgs -join ' ')"
    & dotnet @localArgs
    exit $LASTEXITCODE
}

$dockerCommand = Get-Command docker -ErrorAction SilentlyContinue
if ($null -eq $dockerCommand)
{
    throw "Docker is not available on PATH. Install Docker or run with -UseLocalBuild."
}

$dockerArgs = @(
    "run",
    "--rm",
    "--volume", "${repoRoot}:/work/repo",
    "--volume", "${DalamudLibPath}:/work/dalamud:ro",
    "--volume", "${outputDir}:/output",
    "mcr.microsoft.com/dotnet/sdk:10.0.101",
    "dotnet"
)
$dockerArgs += $msbuildArgs

Write-Host "Running Plogon-like container build..."
Write-Host "docker $($dockerArgs -join ' ')"
& docker @dockerArgs
exit $LASTEXITCODE
