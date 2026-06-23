param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$PreviousTag = '',
    [string]$ReleaseNotesOut = '',
    [string]$ChangelogFile = '',
    [switch]$UpdateChangelog,
    [string]$RepoUrl = '',
    [string]$Date = ''
)

$ErrorActionPreference = 'Stop'

if (-not $Date) { $Date = (Get-Date -Format 'yyyy-MM-dd') }

function Resolve-Tag([string]$v) {
    foreach ($t in @($v, "v$v")) {
        git rev-parse -q --verify "refs/tags/$t^{commit}" > $null 2>&1
        if ($LASTEXITCODE -eq 0) { return $t }
    }
    return 'HEAD'
}

$currentTag = Resolve-Tag $Version

if (-not $PreviousTag) {
    $prev = git describe --tags --abbrev=0 "$currentTag^" 2>$null
    if ($LASTEXITCODE -eq 0 -and $prev) { $PreviousTag = $prev.Trim() }
}

$range = if ($PreviousTag) { "$PreviousTag..$currentTag" } else { $currentTag }

$lines = git log --no-merges --pretty=format:'%h%x1f%s' $range

$order = @('Breaking Changes', 'Features', 'Fixes', 'Performance', 'Refactoring', 'Documentation', 'Build', 'CI', 'Tests', 'Styling', 'Reverts', 'Chores', 'Other')
$typeMap = @{
    feat = 'Features'; fix = 'Fixes'; perf = 'Performance'; refactor = 'Refactoring';
    docs = 'Documentation'; build = 'Build'; ci = 'CI'; test = 'Tests';
    style = 'Styling'; chore = 'Chores'; revert = 'Reverts'
}

$buckets = [ordered]@{}
foreach ($s in $order) { $buckets[$s] = New-Object System.Collections.Generic.List[string] }

foreach ($line in $lines) {
    if (-not $line) { continue }
    $parts = $line.Split([char]31, 2)
    $sha = $parts[0]
    $subject = if ($parts.Count -gt 1) { $parts[1] } else { '' }

    $section = 'Other'
    $entry = $subject

    $m = [regex]::Match($subject, '^(?<type>[a-zA-Z]+)(?<scope>\([^)]*\))?(?<bang>!)?:\s*(?<desc>.+)$')
    if ($m.Success) {
        $type = $m.Groups['type'].Value.ToLower()
        $bang = [bool]$m.Groups['bang'].Value
        if ($bang) {
            $section = 'Breaking Changes'
        }
        elseif ($typeMap.ContainsKey($type)) {
            $section = $typeMap[$type]
        }
        if ($section -ne 'Other') {
            $desc = $m.Groups['desc'].Value
            $scope = $m.Groups['scope'].Value.Trim('(', ')')
            $entry = if ($scope) { "**${scope}:** $desc" } else { $desc }
        }
    }

    $shaText = if ($RepoUrl) { "([``$sha``]($RepoUrl/commit/$sha))" } else { "(``$sha``)" }
    $buckets[$section].Add("- $entry $shaText")
}

$sb = New-Object System.Text.StringBuilder
foreach ($s in $order) {
    if ($buckets[$s].Count -eq 0) { continue }
    [void]$sb.AppendLine("### $s")
    foreach ($i in $buckets[$s]) { [void]$sb.AppendLine($i) }
    [void]$sb.AppendLine('')
}
$sections = $sb.ToString().TrimEnd()
if (-not $sections) { $sections = '_No notable changes._' }

Write-Host $sections

if ($ReleaseNotesOut) {
    $rn = "## Changelog`n`n$sections`n"
    if ($PreviousTag -and $RepoUrl) {
        $rn += "`n**Full changelog:** [$PreviousTag...$currentTag]($RepoUrl/compare/$PreviousTag...$currentTag)`n"
    }
    Set-Content -Path $ReleaseNotesOut -Value $rn -Encoding utf8
}

if ($UpdateChangelog -and $ChangelogFile) {
    $title = '# Changelog'
    $block = "## [$Version] - $Date`n`n$sections`n"

    if (Test-Path $ChangelogFile) {
        $existing = Get-Content -Path $ChangelogFile -Raw
        if ($existing -match "(?m)^## \[$([regex]::Escape($Version))\]") {
            Write-Host "CHANGELOG already contains $Version, skipping."
        }
        else {
            $trimmed = $existing.TrimStart()
            if ($trimmed.StartsWith($title)) {
                $headerEnd = $existing.IndexOf("`n", $existing.IndexOf($title))
                $head = $existing.Substring(0, $headerEnd + 1)
                $rest = $existing.Substring($headerEnd + 1).TrimStart("`r", "`n")
                Set-Content -Path $ChangelogFile -Value "$head`n$block`n$rest" -Encoding utf8
            }
            else {
                Set-Content -Path $ChangelogFile -Value "$title`n`n$block`n$existing" -Encoding utf8
            }
        }
    }
    else {
        Set-Content -Path $ChangelogFile -Value "$title`n`n$block" -Encoding utf8
    }
}
