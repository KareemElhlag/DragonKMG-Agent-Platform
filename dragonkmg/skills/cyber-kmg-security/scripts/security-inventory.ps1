param(
    [string]$Root = (Get-Location).Path,
    [string]$OutFile = ""
)

$ErrorActionPreference = "Stop"
$resolvedRoot = (Resolve-Path -LiteralPath $Root).Path

$patterns = @(
    "Dockerfile",
    "docker-compose*.yml",
    "docker-compose*.yaml",
    ".github/workflows/*.yml",
    ".github/workflows/*.yaml",
    "azure-pipelines*.yml",
    "gitlab-ci*.yml",
    "package.json",
    "pnpm-lock.yaml",
    "package-lock.json",
    "yarn.lock",
    "*.csproj",
    "Directory.Packages.props",
    "pubspec.yaml",
    "pubspec.lock",
    ".env*",
    "appsettings*.json"
)

$hits = foreach ($pattern in $patterns) {
    Get-ChildItem -Path $resolvedRoot -Recurse -Force -File -Filter $pattern -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(security-temp|\.git|node_modules|bin|obj|build)\\' } |
        Select-Object @{Name='Pattern';Expression={$pattern}}, FullName, Length, LastWriteTime
}

$result = [pscustomobject]@{
    Root = $resolvedRoot
    GeneratedAt = Get-Date -Format o
    Count = @($hits).Count
    Files = @($hits)
}

if (-not [string]::IsNullOrWhiteSpace($OutFile)) {
    $parent = Split-Path -Parent $OutFile
    if ($parent) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutFile -Encoding utf8
}

$result | ConvertTo-Json -Depth 5
