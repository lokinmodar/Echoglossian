# Copyright (C) 2026 lokinmodar
# SPDX-License-Identifier: AGPL-3.0-or-later

<#
.SYNOPSIS
Validates the documented open-issue inventory against GitHub.

.DESCRIPTION
Queries open issues with the GitHub CLI, compares their numbers with the
"Complete Open-Issue Inventory" section in docs/github-issue-backlog.md, and
checks the declared total and focused-issue counts.

.EXAMPLE
.\scripts\audit-github-issue-backlog.ps1

.EXAMPLE
.\scripts\audit-github-issue-backlog.ps1 -ShowInventory
#>

[CmdletBinding()]
param(
    [string]$Repository = "lokinmodar/Echoglossian",
    [string]$BacklogPath = (Join-Path $PSScriptRoot "..\docs\github-issue-backlog.md"),
    [switch]$ShowInventory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) is required to audit the issue backlog."
}

$resolvedBacklogPath = (Resolve-Path -LiteralPath $BacklogPath).Path
$issueJson = & gh issue list --repo $Repository --state open --limit 1000 --json number,title,url 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "GitHub issue query failed: $($issueJson -join [Environment]::NewLine)"
}

$openIssues = @($issueJson | ConvertFrom-Json | Sort-Object number)
$backlog = Get-Content -Raw -Encoding utf8 -LiteralPath $resolvedBacklogPath
$inventoryMatch = [regex]::Match(
    $backlog,
    '(?s)## Complete Open-Issue Inventory.*?(?=\r?\n## |\z)')

if (-not $inventoryMatch.Success) {
    throw "Complete Open-Issue Inventory section was not found in $resolvedBacklogPath."
}

$documentedNumbers = @(
    [regex]::Matches($inventoryMatch.Value, '#(?<number>\d+)') |
        ForEach-Object { [int]$_.Groups['number'].Value } |
        Sort-Object -Unique
)
$openNumbers = @($openIssues | ForEach-Object { [int]$_.number })
$missingFromBacklog = @($openNumbers | Where-Object { $_ -notin $documentedNumbers })
$closedButDocumented = @($documentedNumbers | Where-Object { $_ -notin $openNumbers })

$declaredTotalMatch = [regex]::Match(
    $backlog,
    '- Open issues at the current audit head: (?<count>\d+)')
$declaredFocusedMatch = [regex]::Match(
    $backlog,
    '- Focused open issues besides the living tracker \[#12\].*?: (?<count>\d+)')

if (-not $declaredTotalMatch.Success -or -not $declaredFocusedMatch.Success) {
    throw "The published-baseline issue counts could not be parsed from $resolvedBacklogPath."
}

$declaredTotal = [int]$declaredTotalMatch.Groups['count'].Value
$declaredFocused = [int]$declaredFocusedMatch.Groups['count'].Value
$actualTotal = $openIssues.Count
$actualFocused = @($openIssues | Where-Object { [int]$_.number -ne 12 }).Count
$errors = [System.Collections.Generic.List[string]]::new()

if ($missingFromBacklog.Count -gt 0) {
    $errors.Add("Open on GitHub but missing from backlog: $($missingFromBacklog -join ', ')")
}

if ($closedButDocumented.Count -gt 0) {
    $errors.Add("Documented as open but not open on GitHub: $($closedButDocumented -join ', ')")
}

if ($declaredTotal -ne $actualTotal) {
    $errors.Add("Declared total $declaredTotal does not match GitHub total $actualTotal.")
}

if ($declaredFocused -ne $actualFocused) {
    $errors.Add("Declared focused count $declaredFocused does not match GitHub count $actualFocused.")
}

if ($ShowInventory) {
    $openIssues | ForEach-Object {
        Write-Output ("#{0} {1} - {2}" -f $_.number, $_.title, $_.url)
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output "Issue backlog matches GitHub: $actualTotal open issues ($actualFocused focused plus #12)."
