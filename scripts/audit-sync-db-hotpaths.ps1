# Copyright (C) 2026 lokinmodar
# SPDX-License-Identifier: AGPL-3.0-or-later

<#
.SYNOPSIS
Audits synchronous database operations in production runtime source.

.DESCRIPTION
Scans the production source tree for synchronous Entity Framework,
SQLite, persistence-helper, and blocking operation patterns. The current
findings must exactly match the checked-in baseline unless -UpdateBaseline is
used to deliberately refresh that baseline.

.EXAMPLE
.\scripts\audit-sync-db-hotpaths.ps1

.EXAMPLE
.\scripts\audit-sync-db-hotpaths.ps1 -UpdateBaseline `
    -ReportPath .\docs\issue-258-sync-db-hotpath-inventory.md
#>

[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$BaselinePath,
    [string]$ReportPath,
    [switch]$UpdateBaseline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDirectory = Split-Path -Parent $PSCommandPath
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptDirectory '..')).Path
}

if ([string]::IsNullOrWhiteSpace($BaselinePath)) {
    $BaselinePath = Join-Path $scriptDirectory 'sync-db-hotpaths-baseline.json'
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $scriptDirectory '..\artifacts\issue-258\sync-db-hotpath-audit.md'
}

function Get-MigrationStage {
    param([Parameter(Mandatory)][string]$RelativePath)

    switch -Regex ($RelativePath) {
        'ReferenceText|MainCommand' { return 'DB-2' }
        'StringArray' { return 'DB-6' }
        'Quest|ToDo|Journal|ScenarioTree|RecommendList|AreaMap' { return 'DB-3' }
        'Action|Item|Trait|Tooltip' { return 'DB-4' }
        'GameWindow' { return 'DB-5' }
        'DBManagerUI|Echoglossian\.cs' { return 'DB-8' }
        default { return 'DB-7' }
    }
}

function ConvertTo-NormalizedPath {
    param([Parameter(Mandatory)][string]$Path)

    return $Path.Replace('\', '/')
}

function ConvertTo-NormalizedEvidence {
    param([Parameter(Mandatory)][string]$Evidence)

    return [regex]::Replace($Evidence.Trim(), '\s+', ' ')
}

function ConvertTo-CodeOnlyContent {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Content)

    if ([string]::IsNullOrEmpty($Content)) {
        return $Content
    }

    $nonCodePattern = [regex]::new(
        '(?s)/\*.*?\*/|//[^\r\n]*|' +
        '\$*"{3,}.*?"{3,}|' +
        '@"(?:""|[^"])*"|' +
        '"(?:\\.|[^"\\])*"|' +
        '''(?:\\.|[^''\\])*''')
    return $nonCodePattern.Replace(
        $Content,
        {
            param($match)
            return [regex]::Replace($match.Value, '[^\r\n]', ' ')
        })
}

function Test-ExcludedPath {
    param([Parameter(Mandatory)][string]$RelativePath)

    $paddedPath = "/$RelativePath/"
    foreach ($excludedSegment in @(
            '/bin/',
            '/obj/',
            '/.git/',
            '/.worktrees/',
            '/worktrees/',
            '/artifacts/',
            '/vendor/',
            '/scripts/',
            '/.superpowers/',
            '/Echoglossian.Tests/',
            '/Echoglossian.Mock/',
            '/Echoglossian.Mock.Tests/',
            '/Echoglossian.Previewer/',
            '/Echoglossian.Previewer.Tests/',
            '/Echoglossian.Docs/',
            '/EFCoreSqlite/Migrations/')) {
        if ($paddedPath.IndexOf(
                $excludedSegment,
                [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function New-Finding {
    param(
        [Parameter(Mandatory)][string]$Category,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Evidence,
        [Parameter(Mandatory)][int]$Occurrence,
        [Parameter(Mandatory)][int]$Line)

    $normalizedEvidence = ConvertTo-NormalizedEvidence -Evidence $Evidence
    $normalizedEvidence = "$normalizedEvidence [occurrence $Occurrence]"
    $id = "$Category|$RelativePath|$normalizedEvidence"
    return [pscustomobject][ordered]@{
        id = $id
        category = $Category
        stage = Get-MigrationStage -RelativePath $RelativePath
        path = $RelativePath
        evidence = $normalizedEvidence
        line = $Line
    }
}

function Add-MatchesForContent {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[object]]$Findings,
        [Parameter(Mandatory)][string]$Category,
        [Parameter(Mandatory)][regex]$Pattern,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Content)

    $occurrences = @{}
    foreach ($match in $Pattern.Matches($Content)) {
        $matchEvidence = ConvertTo-NormalizedEvidence -Evidence $match.Value
        $occurrence = 1
        if ($occurrences.ContainsKey($matchEvidence)) {
            $occurrence = $occurrences[$matchEvidence] + 1
        }

        $occurrences[$matchEvidence] = $occurrence
        $prefix = $Content.Substring(0, $match.Index)
        $line = 1 + [regex]::Matches($prefix, "\r\n|\r|\n").Count
        $Findings.Add((New-Finding `
            -Category $Category `
            -RelativePath $RelativePath `
            -Evidence $match.Value `
            -Occurrence $occurrence `
            -Line $line)) | Out-Null
    }
}

function Add-TaskResultMatchesForContent {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[object]]$Findings,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Content)

    if ([string]::IsNullOrEmpty($Content)) {
        return
    }

    $taskNames = [System.Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    $taskSourcePatterns = @(
        [regex]::new(
            '\bTask(?:\s*<[^;=]+>)?\s+(?<name>[A-Za-z_]\w*)'),
        [regex]::new(
            '\.ContinueWith\s*\(\s*(?<name>[A-Za-z_]\w*)\s*=>'),
        [regex]::new(
            '(?s)\bvar\s+(?<name>[A-Za-z_]\w*)\s*=\s*[^;]*' +
            '(?:[A-Za-z_]\w*Async\s*\(|Task\s*\.\s*' +
            '(?:Run|Delay|WhenAll|WhenAny|FromResult)\s*\()[^;]*;')
    )
    foreach ($taskSourcePattern in $taskSourcePatterns) {
        foreach ($match in $taskSourcePattern.Matches($Content)) {
            $taskNames.Add($match.Groups['name'].Value) | Out-Null
        }
    }

    $directTaskExpressionPattern = [regex]::new(
        '(?s)(?:[A-Za-z_]\w*Async\s*\([^;]*\)|' +
        'Task\s*\.\s*(?:Run|Delay|WhenAll|WhenAny|FromResult)\s*\([^;]*\)|' +
        '\.ContinueWith\s*\([^;]*\))\s*$')
    $resultPattern = [regex]::new('\.Result\b')
    $receiverPattern = [regex]::new('(?<name>[A-Za-z_]\w*)\s*$')
    $occurrence = 0
    foreach ($match in $resultPattern.Matches($Content)) {
        $statementStart = $Content.LastIndexOf(';', $match.Index)
        $statementPrefix = $Content.Substring(
            $statementStart + 1,
            $match.Index - $statementStart - 1)
        $receiverMatch = $receiverPattern.Match($statementPrefix)
        $isTaskReceiver = $receiverMatch.Success -and
            ($taskNames.Contains($receiverMatch.Groups['name'].Value) -or
             $receiverMatch.Groups['name'].Value.EndsWith(
                 'Task',
                 [StringComparison]::OrdinalIgnoreCase))
        if (-not $isTaskReceiver -and
            -not $directTaskExpressionPattern.IsMatch($statementPrefix)) {
            continue
        }

        $occurrence++
        $prefix = $Content.Substring(0, $match.Index)
        $line = 1 + [regex]::Matches($prefix, "\r\n|\r|\n").Count
        $Findings.Add((New-Finding `
            -Category 'blocking-wait' `
            -RelativePath $RelativePath `
            -Evidence $match.Value `
            -Occurrence $occurrence `
            -Line $line)) | Out-Null
    }
}

function Add-DatabaseQueryMatchesForContent {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[object]]$Findings,
        [Parameter(Mandatory)][regex]$Pattern,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Content)

    if ([string]::IsNullOrEmpty($Content)) {
        return
    }

    $databaseSourceNames = [System.Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal)
    $contextDeclarationPatterns = @(
        [regex]::new(
            '\bEchoglossianDbContext\s*\??\s+(?<name>[A-Za-z_]\w*)'),
        [regex]::new(
            '(?<name>[A-Za-z_]\w*)\s*=\s*new\s+EchoglossianDbContext\s*\(')
    )
    foreach ($contextDeclarationPattern in $contextDeclarationPatterns) {
        foreach ($match in $contextDeclarationPattern.Matches($Content)) {
            $databaseSourceNames.Add($match.Groups['name'].Value) | Out-Null
        }
    }

    if ($databaseSourceNames.Count -eq 0) {
        return
    }

    $queryAliasPattern = [regex]::new(
        '(?s)\b(?:var|IQueryable(?:<[^;=]+>)?|' +
        'IOrderedQueryable(?:<[^;=]+>)?|IEnumerable(?:<[^;=]+>)?|' +
        'IOrderedEnumerable(?:<[^;=]+>)?|DbSet(?:<[^;=]+>)?)' +
        '\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<value>.+)$')
    $foreachPattern = [regex]::new(
        '(?s)\bforeach\s*\([^;{}]*?\bin\b(?<source>[^;{}]*?)\)')
    $occurrences = @{}
    $statementStart = 0
    while ($statementStart -lt $Content.Length) {
        $statementEnd = $Content.IndexOf(';', $statementStart)
        if ($statementEnd -lt 0) {
            $statementEnd = $Content.Length
        }

        $statement = $Content.Substring(
            $statementStart,
            $statementEnd - $statementStart)
        $escapedDatabaseSourceNames = @($databaseSourceNames | ForEach-Object {
            [regex]::Escape($_)
        })
        $databaseSourcePattern = [regex]::new(
            '\b(?:' + ($escapedDatabaseSourceNames -join '|') + ')\b')
        $queryMatches = @($Pattern.Matches($statement))
        $querySegmentStart = 0
        foreach ($match in $queryMatches) {
            $queryPrefix = $statement.Substring(
                $querySegmentStart,
                $match.Index - $querySegmentStart)
            $querySegmentStart = $match.Index + $match.Length
            if ($databaseSourcePattern.IsMatch($queryPrefix)) {
                $matchEvidence = ConvertTo-NormalizedEvidence -Evidence $match.Value
                $occurrence = 1
                if ($occurrences.ContainsKey($matchEvidence)) {
                    $occurrence = $occurrences[$matchEvidence] + 1
                }

                $occurrences[$matchEvidence] = $occurrence
                $absoluteMatchIndex = $statementStart + $match.Index
                $prefix = $Content.Substring(0, $absoluteMatchIndex)
                $line = 1 + [regex]::Matches($prefix, "\r\n|\r|\n").Count
                $Findings.Add((New-Finding `
                    -Category 'sync-ef-query' `
                    -RelativePath $RelativePath `
                    -Evidence $match.Value `
                    -Occurrence $occurrence `
                    -Line $line)) | Out-Null
            }
        }

        $foreachMatches = @($foreachPattern.Matches($statement))
        foreach ($match in $foreachMatches) {
            $foreachSource = $match.Groups['source'].Value
            if (-not $databaseSourcePattern.IsMatch($foreachSource) -or
                $Pattern.IsMatch($foreachSource)) {
                continue
            }

            $matchEvidence = 'foreach ('
            $occurrence = 1
            if ($occurrences.ContainsKey($matchEvidence)) {
                $occurrence = $occurrences[$matchEvidence] + 1
            }

            $occurrences[$matchEvidence] = $occurrence
            $absoluteMatchIndex = $statementStart + $match.Index
            $prefix = $Content.Substring(0, $absoluteMatchIndex)
            $line = 1 + [regex]::Matches($prefix, "\r\n|\r|\n").Count
            $Findings.Add((New-Finding `
                -Category 'sync-ef-query' `
                -RelativePath $RelativePath `
                -Evidence $matchEvidence `
                -Occurrence $occurrence `
                -Line $line)) | Out-Null
        }

        if ($queryMatches.Count -eq 0 -and $foreachMatches.Count -eq 0) {
            $queryAliasMatch = $queryAliasPattern.Match($statement)
            if ($queryAliasMatch.Success -and
                $databaseSourcePattern.IsMatch(
                    $queryAliasMatch.Groups['value'].Value)) {
                $databaseSourceNames.Add(
                    $queryAliasMatch.Groups['name'].Value) | Out-Null
            }
        }

        $statementStart = $statementEnd + 1
    }
}

function Get-BaselineFindings {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return @()
    }

    $baseline = Get-Content -Raw -Encoding utf8 -LiteralPath $Path | ConvertFrom-Json
    if ($baseline.schemaVersion -ne 1) {
        throw "Unsupported synchronous database audit baseline schema version: $($baseline.schemaVersion)."
    }

    return @($baseline.allowedFindings)
}

function Write-Baseline {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Findings,
        [Parameter(Mandatory)][string]$Path)

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('{')
    $lines.Add('  "schemaVersion": 1,')
    $lines.Add('  "allowedFindings": [')
    $sortedFindings = @($Findings | Sort-Object id)
    for ($index = 0; $index -lt $sortedFindings.Count; $index++) {
        $finding = $sortedFindings[$index]
        $suffix = if ($index -lt ($sortedFindings.Count - 1)) { ',' } else { '' }
        $lines.Add('    {')
        $lines.Add(('      "id": "{0}",' -f (ConvertTo-JsonString -Value $finding.id)))
        $lines.Add(('      "category": "{0}",' -f (ConvertTo-JsonString -Value $finding.category)))
        $lines.Add(('      "stage": "{0}",' -f (ConvertTo-JsonString -Value $finding.stage)))
        $lines.Add(('      "path": "{0}",' -f (ConvertTo-JsonString -Value $finding.path)))
        $lines.Add(('      "evidence": "{0}"' -f (ConvertTo-JsonString -Value $finding.evidence)))
        $lines.Add("    }$suffix")
    }

    $lines.Add('  ]')
    $lines.Add('}')
    Write-Utf8NoBom -Path $Path -Content ([string]::Join("`n", $lines) + "`n")
}

function Write-Report {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Findings,
        [Parameter(Mandatory)][string]$Path)

    $reportLines = [System.Collections.Generic.List[string]]::new()
    $reportLines.Add('# Synchronous Database Hot-Path Inventory')
    $reportLines.Add('')
    $reportLines.Add('Generated by `scripts/audit-sync-db-hotpaths.ps1`.')
    $reportLines.Add('')
    $reportLines.Add("Total findings: $($Findings.Count)")

    foreach ($stageGroup in ($Findings | Group-Object stage | Sort-Object Name)) {
        $reportLines.Add('')
        $reportLines.Add("## $($stageGroup.Name)")
        foreach ($pathGroup in ($stageGroup.Group | Group-Object path | Sort-Object Name)) {
            $reportLines.Add('')
            $reportLines.Add("### $($pathGroup.Name)")
            foreach ($finding in ($pathGroup.Group | Sort-Object line, category, evidence, id)) {
                $reportLines.Add(
                    ('- Line {0}: `{1}` - `{2}`' -f `
                        $finding.line,
                        $finding.category,
                        $finding.evidence))
            }
        }
    }

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    Write-Utf8NoBom `
        -Path $Path `
        -Content ([string]::Join("`n", $reportLines) + "`n")
}

function ConvertTo-JsonString {
    param([Parameter(Mandatory)][string]$Value)

    $builder = [System.Text.StringBuilder]::new()
    foreach ($character in $Value.ToCharArray()) {
        $codePoint = [int][char]$character
        if ($codePoint -eq 34) {
            $null = $builder.Append('\"')
        }
        elseif ($codePoint -eq 92) {
            $null = $builder.Append('\\')
        }
        elseif ($codePoint -eq 8) {
            $null = $builder.Append('\b')
        }
        elseif ($codePoint -eq 9) {
            $null = $builder.Append('\t')
        }
        elseif ($codePoint -eq 10) {
            $null = $builder.Append('\n')
        }
        elseif ($codePoint -eq 12) {
            $null = $builder.Append('\f')
        }
        elseif ($codePoint -eq 13) {
            $null = $builder.Append('\r')
        }
        elseif ($codePoint -lt 32) {
            $null = $builder.Append(('\u{0:x4}' -f $codePoint))
        }
        else {
            $null = $builder.Append($character)
        }
    }

    return $builder.ToString()
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Content)

    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false))
}

function Test-EquivalentFindingContract {
    param(
        [Parameter(Mandatory)][object]$CurrentFinding,
        [Parameter(Mandatory)][object]$BaselineFinding)

    foreach ($propertyName in @('id', 'category', 'stage', 'path', 'evidence')) {
        if (-not [string]::Equals(
                [string]$CurrentFinding.$propertyName,
                [string]$BaselineFinding.$propertyName,
                [StringComparison]::Ordinal)) {
            return $false
        }
    }

    return $true
}

$resolvedRepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path.TrimEnd('\', '/')
$directPatterns = @(
    [pscustomobject]@{ Category = 'sync-ef-save'; Pattern = '(?<!Async)\bSaveChanges\s*\(' },
    [pscustomobject]@{ Category = 'sync-ef-transaction'; Pattern = '(?<!Async)\bBeginTransaction\s*\(' },
    [pscustomobject]@{ Category = 'sync-ef-migrate'; Pattern = '\.Database\.(?:Migrate|EnsureCreated)\s*\(' },
    [pscustomobject]@{
        Category = 'sync-sql-command'
        Pattern = '\bExecute(?:SqlRaw|SqlInterpolated|NonQuery|Reader|Scalar)\s*\('
    },
    [pscustomobject]@{ Category = 'sync-ef-bulk-write'; Pattern = '\bExecute(?:Update|Delete)\s*\(' },
    [pscustomobject]@{ Category = 'direct-db-context'; Pattern = '\bnew\s+EchoglossianDbContext\s*\(' },
    [pscustomobject]@{
        Category = 'blocking-wait'
        Pattern = '(?:\.Wait\s*\(|' +
            '\.GetAwaiter\s*\(\s*\)\s*\.GetResult\s*\()'
    }
)

$persistenceHelperPattern = [regex]::new(
    '(?<helper>ReferenceTextPersistenceHelper|ActionTooltipPersistenceHelper|' +
    'ItemTooltipPersistenceHelper|TraitPersistenceHelper|' +
    'StringArrayDataPersistenceHelper|GameWindowPersistenceHelper|' +
    'TranslationFailurePersistenceHelper|LlmCapabilityPersistenceHelper)' +
    '\s*\.\s*(?<method>Find\w*|Insert\w*|Record\w*|Upsert\w*)\s*\(')

$databaseQueryPattern = [regex]::new(
    '(?<!Async)\b(?:Aggregate|All|Any|Average|Contains|Count|ElementAt|' +
    'ElementAtOrDefault|Find|First|FirstOrDefault|Last|LastOrDefault|' +
    'LongCount|Max|Min|Single|SingleOrDefault|Sum|ToArray|ToDictionary|' +
    'ToHashSet|ToList|ToLookup)\s*\(')

$sourceFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
foreach ($sourceFile in Get-ChildItem `
        -LiteralPath $resolvedRepositoryRoot `
        -Recurse `
        -File `
        -Filter '*.cs') {
    $sourceFiles.Add($sourceFile)
}

$findings = [System.Collections.Generic.List[object]]::new()
foreach ($sourceFile in ($sourceFiles | Sort-Object FullName)) {
    $relativePath = ConvertTo-NormalizedPath -Path $sourceFile.FullName.Substring($resolvedRepositoryRoot.Length).TrimStart('\', '/')
    if (Test-ExcludedPath -RelativePath $relativePath) {
        continue
    }

    $sourceContent = Get-Content -Raw -Encoding utf8 -LiteralPath $sourceFile.FullName
    $content = ConvertTo-CodeOnlyContent -Content $sourceContent
    foreach ($directPattern in $directPatterns) {
        Add-MatchesForContent `
            -Findings $findings `
            -Category $directPattern.Category `
            -Pattern ([regex]::new($directPattern.Pattern)) `
            -RelativePath $relativePath `
            -Content $content
    }

    Add-TaskResultMatchesForContent `
        -Findings $findings `
        -RelativePath $relativePath `
        -Content $content

    Add-MatchesForContent `
        -Findings $findings `
        -Category 'persistence-helper-call' `
        -Pattern $persistenceHelperPattern `
        -RelativePath $relativePath `
        -Content $content

    Add-DatabaseQueryMatchesForContent `
        -Findings $findings `
        -Pattern $databaseQueryPattern `
        -RelativePath $relativePath `
        -Content $content
}

$currentFindings = @($findings | Sort-Object id, line -Unique)
Write-Report -Findings $currentFindings -Path $ReportPath

if ($UpdateBaseline) {
    Write-Baseline -Findings $currentFindings -Path $BaselinePath
}

$baselineFindings = @(Get-BaselineFindings -Path $BaselinePath)
$baselineById = [System.Collections.Generic.Dictionary[string, object]]::new(
    [StringComparer]::Ordinal)
foreach ($finding in $baselineFindings) {
    $baselineById[[string]$finding.id] = $finding
}

$currentById = [System.Collections.Generic.Dictionary[string, object]]::new(
    [StringComparer]::Ordinal)
foreach ($finding in $currentFindings) {
    $currentById[[string]$finding.id] = $finding
}

$unexpected = @($currentFindings | Where-Object {
        -not $baselineById.ContainsKey([string]$_.id)
    })
$resolved = @($baselineFindings | Where-Object {
        -not $currentById.ContainsKey([string]$_.id)
    })
$changed = @($baselineFindings | Where-Object {
        $currentById.ContainsKey([string]$_.id) -and
        -not (Test-EquivalentFindingContract `
            -CurrentFinding $currentById[[string]$_.id] `
            -BaselineFinding $_)
    })

if ($unexpected.Count -gt 0 -or $resolved.Count -gt 0 -or $changed.Count -gt 0) {
    $messages = [System.Collections.Generic.List[string]]::new()
    foreach ($finding in $unexpected) {
        $messages.Add("unexpected sync database finding: $($finding.id)")
    }

    foreach ($finding in $resolved) {
        $messages.Add("resolved baseline finding: $($finding.id)")
    }

    foreach ($finding in $changed) {
        $messages.Add("changed baseline finding: $($finding.id)")
    }

    throw ($messages -join [Environment]::NewLine)
}

Write-Host "Synchronous database hot-path audit passed: $($currentFindings.Count) finding(s)."
foreach ($stageGroup in ($currentFindings | Group-Object stage | Sort-Object Name)) {
    Write-Host "$($stageGroup.Name): $($stageGroup.Count)"
}
