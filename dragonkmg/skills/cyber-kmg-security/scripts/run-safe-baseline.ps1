param(
    [string]$Root = (Get-Location).Path,
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$resolvedRoot = (Resolve-Path -LiteralPath $Root).Path

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutDir = Join-Path $resolvedRoot "security-temp\$timestamp-safe-baseline"
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$summary = [ordered]@{
    Root = $resolvedRoot
    GeneratedAt = Get-Date -Format o
    Checks = @()
}

function Add-Check {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Details,
        [string]$Artifact = ""
    )

    $summary.Checks += [ordered]@{
        Name = $Name
        Status = $Status
        Details = $Details
        Artifact = $Artifact
    }
}

function Save-Lines {
    param(
        [string]$Name,
        [object[]]$Lines
    )

    $path = Join-Path $OutDir $Name
    $Lines | Out-File -LiteralPath $path -Encoding utf8
    return $path
}

$excluded = '\\(security-temp|\.git|node_modules|bin|obj|build|\.dart_tool)\\'

$secretPatterns = @(
    'password\s*[:=]\s*["''][^"'']{8,}',
    'api[_-]?key\s*[:=]\s*["''][^"'']{12,}',
    'secret\s*[:=]\s*["''][^"'']{12,}',
    'connectionstring\s*[:=]',
    '-----BEGIN (RSA |EC |OPENSSH |PRIVATE )?PRIVATE KEY-----',
    # A real secret tracked in this project's own history (a live reCAPTCHA key, 2026) sat behind a
    # JSON key named "...SecretKey" and was missed by the bare 'secret[:=]' pattern above, because
    # "Secret" there is a substring of the KEY NAME, not immediately followed by ':'. This pattern
    # catches that shape: any key ending in Secret/Key/Token/Password with a real-looking value
    # (quoted, 12+ chars, not empty and not a ${...}/%...% placeholder).
    # No \s allowed in the value - excludes i18n label text like "apiKey": "مفتاح API مخصص..."
    '"[A-Za-z0-9_]*(Secret|ApiKey|Token|Password)[A-Za-z0-9_]*"\s*:\s*"(?!\s*$|\$\{|%)[^"\s]{12,}"',
    # Cloud/provider key shapes with a recognizable, low-false-positive prefix.
    'AKIA[0-9A-Z]{16}',
    'AIza[0-9A-Za-z_\-]{35}',
    'sk_live_[0-9a-zA-Z]{16,}',
    'ghp_[0-9A-Za-z]{36}'
)

$textFiles = Get-ChildItem -Path $resolvedRoot -Recurse -Force -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch $excluded -and $_.Length -lt 2MB }

$secretHits = foreach ($file in $textFiles) {
    foreach ($pattern in $secretPatterns) {
        Select-String -LiteralPath $file.FullName -Pattern $pattern -AllMatches -ErrorAction SilentlyContinue |
            ForEach-Object {
                "{0}:{1}: potential secret pattern '{2}'" -f $_.Path, $_.LineNumber, $pattern
            }
    }
}

$secretArtifact = Save-Lines -Name "potential-secret-patterns.txt" -Lines $secretHits
Add-Check -Name "Secret pattern scan" -Status "COMPLETED" -Details ("Potential matches: {0}" -f @($secretHits).Count) -Artifact $secretArtifact

$dockerFiles = Get-ChildItem -Path $resolvedRoot -Recurse -Force -File -Include "Dockerfile","docker-compose*.yml","docker-compose*.yaml" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch $excluded }

$dockerPatterns = @(
    'privileged:\s*true',
    '/var/run/docker\.sock',
    'user:\s*root',
    '^\s*USER\s+root\b',
    'ports:\s*$',
    '0\.0\.0\.0:',
    'latest\b'
)

$dockerHits = foreach ($file in $dockerFiles) {
    foreach ($pattern in $dockerPatterns) {
        Select-String -LiteralPath $file.FullName -Pattern $pattern -AllMatches -ErrorAction SilentlyContinue |
            ForEach-Object {
                "{0}:{1}: docker risk pattern '{2}' => {3}" -f $_.Path, $_.LineNumber, $pattern, $_.Line.Trim()
            }
    }
}

$dockerArtifact = Save-Lines -Name "docker-risk-patterns.txt" -Lines $dockerHits
Add-Check -Name "Docker risk pattern scan" -Status "COMPLETED" -Details ("Potential matches: {0}" -f @($dockerHits).Count) -Artifact $dockerArtifact

$manifestPatterns = @("package.json","pnpm-lock.yaml","package-lock.json","yarn.lock","*.csproj","Directory.Packages.props","pubspec.yaml","pubspec.lock")
$manifestHits = foreach ($pattern in $manifestPatterns) {
    Get-ChildItem -Path $resolvedRoot -Recurse -Force -File -Filter $pattern -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch $excluded } |
        ForEach-Object { $_.FullName }
}

$manifestArtifact = Save-Lines -Name "dependency-manifests.txt" -Lines $manifestHits
Add-Check -Name "Dependency manifest inventory" -Status "COMPLETED" -Details ("Manifests found: {0}" -f @($manifestHits).Count) -Artifact $manifestArtifact

$summaryPath = Join-Path $OutDir "safe-baseline-summary.json"
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding utf8

[pscustomobject]@{
    OutDir = $OutDir
    Summary = $summaryPath
    SecretMatches = @($secretHits).Count
    DockerRiskMatches = @($dockerHits).Count
    DependencyManifests = @($manifestHits).Count
} | ConvertTo-Json
