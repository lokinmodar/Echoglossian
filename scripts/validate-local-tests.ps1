Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

Push-Location $repoRoot
try
{
    & .\scripts\audit-sync-db-hotpaths.ps1 `
        -ReportPath .\artifacts\issue-258\sync-db-hotpath-audit.md

    dotnet restore .\Echoglossian.Tests\Echoglossian.Tests.csproj
    dotnet build .\Echoglossian.sln -c Debug --no-restore
    dotnet build .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore
    dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build

    dotnet restore .\Echoglossian.Mock\Echoglossian.Mock.csproj
    dotnet build .\Echoglossian.Mock\Echoglossian.Mock.csproj -c Debug --no-restore

    dotnet restore .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj
    dotnet build .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
    dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build
}
finally
{
    Pop-Location
}
