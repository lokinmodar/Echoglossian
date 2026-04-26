param(
    [Parameter(Mandatory = $true)]
    [string]$MigrationName,
    [string]$Configuration = "Debug",
    [string]$Context = "EchoglossianDbContext",
    [string]$DalamudLibPath = "C:\Users\lokin\AppData\Roaming\XIVLauncher\addon\Hooks\dev"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$projectPath = Join-Path $repoRoot "Echoglossian.csproj"
$outputDir = Join-Path $repoRoot "bin\x64\$Configuration\win-x64"
$assemblyPatterns = @(
    "Dalamud*.dll",
    "FFXIVClientStructs.dll",
    "InteropGenerator.Runtime.dll",
    "Lumina.dll",
    "Lumina.Excel.dll",
    "Newtonsoft.Json.dll",
    "Serilog.dll",
    "Microsoft.Extensions.ObjectPool.dll"
)

if (-not (Test-Path -LiteralPath $DalamudLibPath))
{
    throw "DalamudLibPath not found: $DalamudLibPath"
}

Write-Host "Building project for EF migration scaffolding..."
& dotnet build $projectPath `
    -c $Configuration `
    --no-restore
if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$copiedDestinations = New-Object System.Collections.Generic.List[string]
foreach ($pattern in $assemblyPatterns)
{
    foreach ($sourceFile in Get-ChildItem -Path $DalamudLibPath -Filter $pattern -File -ErrorAction SilentlyContinue)
    {
        $destinationPath = Join-Path $outputDir $sourceFile.Name
        if (-not (Test-Path -LiteralPath $destinationPath))
        {
            $copiedDestinations.Add($destinationPath)
        }

        Copy-Item -LiteralPath $sourceFile.FullName -Destination $destinationPath -Force
    }
}

try
{
    Write-Host "Scaffolding EF migration '$MigrationName'..."
    & dotnet ef migrations add $MigrationName `
        --context $Context `
        --no-build
    exit $LASTEXITCODE
}
finally
{
    foreach ($destinationPath in $copiedDestinations)
    {
        if (Test-Path -LiteralPath $destinationPath)
        {
            Remove-Item -LiteralPath $destinationPath -Force
        }
    }
}
