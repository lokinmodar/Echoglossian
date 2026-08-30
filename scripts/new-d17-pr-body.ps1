# Copyright (C) 2026 lokinmodar
# SPDX-License-Identifier: AGPL-3.0-or-later

<#
.SYNOPSIS
Generates a Discord-webhook-safe official DalamudPluginsD17 PR body.

.DESCRIPTION
Builds the repo-approved Markdown subset for official D17 pull requests so the
same text renders predictably on GitHub and in Discord webhook mirrors.

.EXAMPLE
.\scripts\new-d17-pr-body.ps1 `
    -Version "v4.2601.0830.1200" `
    -EchoglossianPrUrl "https://github.com/lokinmodar/Echoglossian/pull/310" `
    -ReleaseTagUrl "https://github.com/lokinmodar/Echoglossian/releases/tag/v4.2601.0830.1200" `
    -IssueUrl "https://github.com/lokinmodar/Echoglossian/issues/274" `
    -SummaryLine "pull in the merged RTL overlay fix" `
    -ValidationLine "local release build completed"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$EchoglossianPrUrl,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseTagUrl,

    [string[]]$IssueUrl = @(),

    [string[]]$SummaryLine = @(),

    [string[]]$ValidationLine = @(),

    [ValidateSet("Assist", "Pair", "Copilot", "Auto")]
    [string]$AiDisclosureLevel,

    [string[]]$AiScopeLine = @(),

    [string[]]$HumanVerificationLine = @(),

    [switch]$IncludeAssetDisclosure,

    [string[]]$AssetDisclosureLine = @(),

    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "d17-pr-body\D17PrBody.csproj"
$args = @(
    "run",
    "--project",
    $projectPath,
    "--",
    "--version",
    $Version,
    "--echoglossian-pr-url",
    $EchoglossianPrUrl,
    "--release-tag-url",
    $ReleaseTagUrl
)

foreach ($item in $IssueUrl)
{
    $args += @("--issue-url", $item)
}

foreach ($item in $SummaryLine)
{
    $args += @("--summary", $item)
}

foreach ($item in $ValidationLine)
{
    $args += @("--validation", $item)
}

if ($AiDisclosureLevel)
{
    $args += @("--ai-disclosure-level", $AiDisclosureLevel)
}

foreach ($item in $AiScopeLine)
{
    $args += @("--ai-scope", $item)
}

foreach ($item in $HumanVerificationLine)
{
    $args += @("--human-verification", $item)
}

if ($IncludeAssetDisclosure.IsPresent -or $AssetDisclosureLine.Count -gt 0)
{
    $args += "--include-asset-disclosure"
}

foreach ($item in $AssetDisclosureLine)
{
    $args += @("--asset-disclosure", $item)
}

if ($OutputPath)
{
    $args += @("--output", $OutputPath)
}

$exitCode = 0
$repoRoot = Split-Path -Parent $PSScriptRoot
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
