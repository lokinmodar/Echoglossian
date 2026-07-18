Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$mockRunnerPath = Join-Path $repoRoot 'Echoglossian.Mock\bin\x64\Debug\win-x64\Echoglossian.Mock.exe'

Push-Location $repoRoot
try
{
    dotnet restore .\Echoglossian.Tests\Echoglossian.Tests.csproj
    dotnet build .\Echoglossian.sln -c Debug --no-restore
    dotnet build .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore
    dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build

    dotnet restore .\Echoglossian.Mock\Echoglossian.Mock.csproj
    dotnet build .\Echoglossian.Mock\Echoglossian.Mock.csproj -c Debug --no-restore

    dotnet restore .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj
    dotnet build .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore

    & $mockRunnerPath --check-compatibility
    if ($LASTEXITCODE -eq 0)
    {
        dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build
    }
    elseif ($LASTEXITCODE -eq 1)
    {
        Write-Warning 'Skipping Echoglossian.Mock.Tests because the local Dalamud hook requires IFramework.CreateDebouncer and DalaMock.Core 6.1.7 does not advertise that contract yet.'
    }
    else
    {
        throw "Unexpected DalaMock compatibility probe exit code: $LASTEXITCODE"
    }
}
finally
{
    Pop-Location
}
