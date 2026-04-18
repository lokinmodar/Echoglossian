param(
    [string]$DatabasePath = "",
    [string[]]$Tables = @("gamewindows", "stringarraydatas"),
    [string]$Sql = "",
    [int]$Limit = 10,
    [switch]$SkipSchema,
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptDir "db-inspector\DbInspector.csproj"

if (-not (Test-Path -LiteralPath $projectPath))
{
    throw "DB inspector project not found: $projectPath"
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

foreach ($table in $Tables)
{
    $args += @("--table", $table)
}

if (-not [string]::IsNullOrWhiteSpace($Sql))
{
    $args += @("--sql", $Sql)
}

$args += @("--limit", $Limit.ToString())

if ($SkipSchema)
{
    $args += "--skip-schema"
}

if ($AsJson)
{
    $args += "--json"
}

& dotnet @args
exit $LASTEXITCODE
