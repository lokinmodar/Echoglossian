param(
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "translation-surface-docs\TranslationSurfaceDocs.csproj"
$args = @("run", "--project", $projectPath, "--")
$repoRoot = Split-Path -Parent $PSScriptRoot

if ($ValidateOnly)
{
    $args += "--validate-only"
}

$exitCode = 0
Push-Location $repoRoot
try
{
    & dotnet @args
    $exitCode = $LASTEXITCODE
}
finally
{
    Pop-Location
}

exit $exitCode
