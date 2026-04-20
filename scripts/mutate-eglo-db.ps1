param(
    [string]$DatabasePath = "",
    [Parameter(Mandatory = $true)]
    [string]$Sql,
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptDir "db-executor\DbExecutor.csproj"

if (-not (Test-Path -LiteralPath $projectPath))
{
    throw "DB executor project not found: $projectPath"
}

$args = @(
    "run",
    "--project", $projectPath,
    "--"
)

if (-not [string]::IsNullOrWhiteSpace($DatabasePath))
{
    $args += @("--database-path", $DatabasePath)
}

$args += @("--sql", $Sql)

if ($AsJson)
{
    $args += "--json"
}

& dotnet @args
exit $LASTEXITCODE
