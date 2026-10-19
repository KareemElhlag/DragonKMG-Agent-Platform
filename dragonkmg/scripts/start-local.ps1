[CmdletBinding()]
param(
    [switch] $WithCli,
    [switch] $CheckOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$uiRoot = Join-Path $repoRoot 'ui'
$apiProject = Join-Path $repoRoot 'src\DragonKMG.Api\DragonKMG.Api.csproj'
$cliProject = Join-Path $repoRoot 'src\DragonKMG.Cli\DragonKMG.Cli.csproj'

function Get-CommandPath([string] $Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) { throw "Required command '$Name' was not found. Install the .NET 9 SDK and Node.js 20.19+ (or 22.12+), then retry." }
    return $command.Source
}

function Test-LoopbackPort([int] $Port) {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
    try { $listener.Start(); return $true }
    catch { return $false }
    finally { $listener.Stop() }
}

function ConvertTo-PsLiteral([string] $Value) { return "'" + $Value.Replace("'", "''") + "'" }

function Start-TerminalProcess([string] $PowerShell, [string] $Title, [string] $Command, [string] $WorkingDirectory) {
    $script = "`$Host.UI.RawUI.WindowTitle = $(ConvertTo-PsLiteral $Title); Set-Location -LiteralPath $(ConvertTo-PsLiteral $WorkingDirectory); $Command"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
    Start-Process -FilePath $PowerShell -ArgumentList @('-NoProfile', '-NoExit', '-EncodedCommand', $encoded) | Out-Null
}

$dotnet = Get-CommandPath 'dotnet'
$node = Get-CommandPath 'node'
$null = Get-CommandPath 'npm'
$npm = 'npm'
$dotnetVersion = (& $dotnet --version).Trim()
if ($dotnetVersion -notmatch '^9\.') { throw "DragonKMG requires the .NET 9 SDK; found '$dotnetVersion'." }
$nodeVersionText = (& $node --version).Trim().TrimStart('v')
try { $nodeVersion = [version]$nodeVersionText } catch { throw "Could not parse Node.js version '$nodeVersionText'." }
if (-not (($nodeVersion.Major -eq 20 -and $nodeVersion -ge [version]'20.19.0') -or ($nodeVersion.Major -ge 22 -and $nodeVersion -ge [version]'22.12.0'))) {
    throw "Vite requires Node.js 20.19+ or 22.12+; found '$nodeVersionText'."
}
if (-not (Test-Path -LiteralPath $apiProject) -or -not (Test-Path -LiteralPath $cliProject)) { throw 'Run this script from a complete DragonKMG source checkout.' }

$localData = Join-Path $env:LOCALAPPDATA 'DragonKMG'
$preferredDb = Join-Path $localData 'dragonkmg.db'
$existingDbs = @(
    (Join-Path $repoRoot 'src\DragonKMG.Api\bin\Debug\net9.0\dragonkmg.db'),
    (Join-Path $repoRoot 'src\DragonKMG.Api\bin\Release\net9.0\dragonkmg.db'),
    $preferredDb
) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | ForEach-Object { Get-Item -LiteralPath $_ } | Sort-Object LastWriteTime -Descending
$dbPath = if (-not [string]::IsNullOrWhiteSpace($env:DRAGONKMG_DB_PATH)) {
    [System.IO.Path]::GetFullPath($env:DRAGONKMG_DB_PATH)
} elseif ($existingDbs.Count -gt 0) {
    $existingDbs[0].FullName
} else {
    $preferredDb
}

if (-not (Test-Path -LiteralPath (Join-Path $uiRoot 'node_modules\vite'))) {
    if ($CheckOnly) { Write-Host 'Dashboard dependencies are missing; a normal start will install them from package-lock.json.' }
    else {
        Write-Host 'Installing dashboard dependencies from the locked package list...'
        Push-Location $uiRoot
        try { & $npm ci; if ($LASTEXITCODE -ne 0) { throw 'Dashboard dependency installation failed.' } }
        finally { Pop-Location }
    }
}

Write-Host "DragonKMG local setup check passed (.NET $dotnetVersion, Node.js $nodeVersionText)."
Write-Host "Shared session database: $dbPath"
if ($CheckOnly) { Write-Host 'Check-only mode: no services were started and no token was created.'; return }

foreach ($port in 5188, 5173) {
    if (-not (Test-LoopbackPort $port)) { throw "Port $port is already in use. Close the application using it and retry; DragonKMG will not stop existing processes." }
}
$null = New-Item -ItemType Directory -Path (Split-Path -Parent $dbPath) -Force

$shell = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
if (-not $shell) { $shell = (Get-Command powershell -ErrorAction SilentlyContinue).Source }
$tokenBytes = [byte[]]::new(32)
$random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $random.GetBytes($tokenBytes) } finally { $random.Dispose() }
$token = [Convert]::ToBase64String($tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
$apiCommand = "`$env:ASPNETCORE_ENVIRONMENT = 'Development'; `$env:DRAGONKMG_DB_PATH = $(ConvertTo-PsLiteral $dbPath); `$env:DRAGONKMG_URL = 'http://127.0.0.1:5188'; & $(ConvertTo-PsLiteral $dotnet) run --project $(ConvertTo-PsLiteral $apiProject) --no-launch-profile"
$env:DRAGONKMG_LOCAL_API_TOKEN = $token
try { Start-TerminalProcess $shell 'DragonKMG API' $apiCommand $repoRoot }
finally { Remove-Item Env:\DRAGONKMG_LOCAL_API_TOKEN -ErrorAction SilentlyContinue }

$ready = $false
for ($attempt = 0; $attempt -lt 60; $attempt++) {
    Start-Sleep -Seconds 1
    try {
        $health = Invoke-RestMethod -Uri 'http://127.0.0.1:5188/health' -TimeoutSec 2
        if ($health.status -eq 'ok') { $ready = $true; break }
    } catch { }
}
if (-not $ready) { throw 'The API did not become healthy within 60 seconds. Check the DragonKMG API terminal for the startup error; no existing data was changed by this launcher.' }

$uiCommand = "`$env:DRAGONKMG_LOCAL_API_TOKEN = `$null; & $(ConvertTo-PsLiteral $npm) run dev"
Start-TerminalProcess $shell 'DragonKMG Dashboard' $uiCommand $uiRoot
Start-Process 'http://127.0.0.1:5173/'

if ($WithCli) {
    $cliCommand = "`$env:DRAGONKMG_DB_PATH = $(ConvertTo-PsLiteral $dbPath); & $(ConvertTo-PsLiteral $dotnet) run --project $(ConvertTo-PsLiteral $cliProject) -- tui"
    Start-TerminalProcess $shell 'DragonKMG Terminal' $cliCommand $repoRoot
}

Write-Host ''
Write-Host 'DragonKMG is ready. In the dashboard, paste this temporary local access token:' -ForegroundColor Green
Write-Host $token -ForegroundColor Cyan
Write-Host 'The token is generated for this launch only and is not saved to a file. Keep this terminal private.'
Write-Host 'Close the API and Dashboard terminal windows when you finish; the selected session database is persistent.'
