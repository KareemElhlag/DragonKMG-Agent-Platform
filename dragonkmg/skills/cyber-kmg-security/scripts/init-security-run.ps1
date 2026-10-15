param(
    [string]$Scope = "security",
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Add-IgnoreEntry {
    param(
        [string]$Path,
        [string]$Entry
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Set-Content -LiteralPath $Path -Value "$Entry`n" -Encoding utf8
        return
    }

    $content = Get-Content -LiteralPath $Path -Raw
    $lines = $content -split "`r?`n"
    if ($lines -notcontains $Entry) {
        Add-Content -LiteralPath $Path -Value $Entry -Encoding utf8
    }
}

$resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
$safeScope = ($Scope.ToLowerInvariant() -replace '[^a-z0-9._-]+', '-').Trim('-')
if ([string]::IsNullOrWhiteSpace($safeScope)) {
    $safeScope = "security"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runId = "$timestamp-$safeScope"
$evidenceRoot = Join-Path $resolvedRoot "security-temp"
$runRoot = Join-Path $evidenceRoot $runId

New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
Add-IgnoreEntry -Path (Join-Path $resolvedRoot ".gitignore") -Entry "security-temp/"
Add-IgnoreEntry -Path (Join-Path $resolvedRoot ".dockerignore") -Entry "security-temp/"

$record = @"
# Security Run Record

- Run id: $runId
- Date/time: $(Get-Date -Format o)
- Scope: $Scope
- Root: $resolvedRoot
- Authorization:
- Environment:
- Exclusions:
- Tools and versions:
- Checks run:
- Checks blocked:
- Findings:
- Residual risk:
"@

$recordPath = Join-Path $runRoot "run-record.md"
Set-Content -LiteralPath $recordPath -Value $record -Encoding utf8

[pscustomobject]@{
    RunId = $runId
    EvidenceRoot = $runRoot
    RunRecord = $recordPath
    GitIgnore = Join-Path $resolvedRoot ".gitignore"
    DockerIgnore = Join-Path $resolvedRoot ".dockerignore"
} | ConvertTo-Json
