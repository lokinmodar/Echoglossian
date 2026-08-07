param(
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "translation-surface-docs\TranslationSurfaceDocs.csproj"
$args = @("run", "--project", $projectPath, "--")

if ($ValidateOnly)
{
    $args += "--validate-only"
}

& dotnet @args
exit $LASTEXITCODE
