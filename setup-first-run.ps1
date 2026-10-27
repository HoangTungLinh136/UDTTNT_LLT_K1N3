$ErrorActionPreference = "Stop"

# Prevent a known Windows CLI shutdown failure in the workload advertising
# logger from turning a successful restore/build into a non-zero exit code.
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = "true"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "true"
$env:DOTNET_SDK_VULNERABILITY_CHECK_DISABLE = "true"
$env:DOTNET_NOLOGO = "true"

$root = $PSScriptRoot
$avaRoot = Join-Path $root "ava_service"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "HRMS + AVA - FIRST RUN SETUP"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Find ASP.NET project without hard-coding a Unicode folder name.
$webProject = Get-ChildItem -LiteralPath $root -Recurse -File -Filter *.csproj |
    Where-Object { $_.FullName -notmatch '\\tests\\' } |
    Select-Object -First 1

if (-not $webProject) {
    throw "Cannot find ASP.NET project (*.csproj)."
}

$webRoot = $webProject.Directory.FullName

Write-Host "ROOT = $root"
Write-Host "AVA  = $avaRoot"
Write-Host "WEB  = $webRoot"
Write-Host ""

# ------------------------------------------------------------
# 1/7 - Python 3.11
# ------------------------------------------------------------

Write-Host "[1/7] Checking Python 3.11..." -ForegroundColor Cyan

$pyCheck = & py -3.11 --version 2>&1

if ($LASTEXITCODE -ne 0) {
    throw "Python 3.11 was not found."
}

Write-Host $pyCheck -ForegroundColor Green

# ------------------------------------------------------------
# 2/7 - .NET 9 SDK
# ------------------------------------------------------------

Write-Host "[2/7] Checking .NET 9 SDK..." -ForegroundColor Cyan

$sdks = & dotnet --list-sdks 2>&1

if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '(?m)^9\.')) {
    throw ".NET 9 SDK was not found."
}

Write-Host ".NET 9 SDK = OK" -ForegroundColor Green

# ------------------------------------------------------------
# 3/7 - Python virtual environment
# ------------------------------------------------------------

Write-Host "[3/7] Preparing Python virtual environment..." -ForegroundColor Cyan

$venv = Join-Path $avaRoot ".venv"
$python = Join-Path $venv "Scripts\python.exe"

if (-not (Test-Path $python)) {
    Push-Location $avaRoot

    try {
        & py -3.11 -m venv .venv

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create Python virtual environment."
        }
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path $python)) {
    throw "Python virtual environment is missing."
}

Write-Host ".venv = OK" -ForegroundColor Green

# ------------------------------------------------------------
# 4/7 - Python packages
# ------------------------------------------------------------

Write-Host "[4/7] Installing Python packages..." -ForegroundColor Cyan

$requirements = Join-Path $avaRoot "requirements.txt"

if (-not (Test-Path $requirements)) {
    throw "requirements.txt was not found."
}

& $python -m pip install -q -r $requirements

if ($LASTEXITCODE -ne 0) {
    throw "pip install failed."
}

Write-Host "Python packages = OK" -ForegroundColor Green

# ------------------------------------------------------------
# 5/7 - .env
# ------------------------------------------------------------

Write-Host "[5/7] Preparing .env..." -ForegroundColor Cyan

$envExample = Join-Path $avaRoot ".env.example"
$envFile = Join-Path $avaRoot ".env"

if (-not (Test-Path $envExample)) {
    throw ".env.example was not found."
}

if (-not (Test-Path $envFile)) {

    Copy-Item $envExample $envFile -Force

    $bytes = New-Object byte[] 48
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()

    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }

    $secret = [Convert]::ToBase64String($bytes)

    $envContent = [System.IO.File]::ReadAllText($envFile)

    $envContent = [regex]::Replace(
        $envContent,
        '(?m)^AVA_INTERNAL_SECRET=.*$',
        "AVA_INTERNAL_SECRET=$secret"
    )

    [System.IO.File]::WriteAllText(
        $envFile,
        $envContent,
        [System.Text.UTF8Encoding]::new($false)
    )

    Write-Host ".env created." -ForegroundColor Green
}
else {
    Write-Host ".env already exists. Keeping current configuration." -ForegroundColor Green
}

# ------------------------------------------------------------
# 6/7 - ASP.NET restore/build
# ------------------------------------------------------------

Write-Host "[6/7] Restoring ASP.NET packages..." -ForegroundColor Cyan

Push-Location $webRoot

try {
    & dotnet restore $webProject.FullName

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed."
    }

    & dotnet build $webProject.FullName --no-restore

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed."
    }
}
finally {
    Pop-Location
}

Write-Host "ASP.NET build = OK" -ForegroundColor Green

# ------------------------------------------------------------
# 7/7 - External dependencies
# ------------------------------------------------------------

# Refresh PATH from registry so newly installed tools (Ollama, FFmpeg) are found immediately
foreach ($scope in @('User', 'Machine')) {
    $rawPath = [System.Environment]::GetEnvironmentVariable('Path', $scope)
    if ($rawPath) {
        foreach ($folder in $rawPath.Split(';')) {
            if ($folder -and (Test-Path $folder) -and ($env:PATH -split ';' -notcontains $folder)) {
                $env:PATH = "$folder;$env:PATH"
            }
        }
    }
}
if ((Test-Path 'D:\Ollama\ollama.exe') -and ($env:PATH -split ';' -notcontains 'D:\Ollama')) {
    $env:PATH = "D:\Ollama;$env:PATH"
}

Write-Host "[7/7] Checking external dependencies..." -ForegroundColor Cyan
Write-Host ""

$ollamaCmd = Get-Command ollama -ErrorAction SilentlyContinue

if ($ollamaCmd) {
    $ollamaVersion = & ollama --version 2>&1
    Write-Host "Ollama = OK ($ollamaVersion)" -ForegroundColor Green
}
else {
    Write-Host "Ollama = NOT FOUND" -ForegroundColor Yellow
}

$ffmpegCmd = Get-Command ffmpeg -ErrorAction SilentlyContinue

if ($ffmpegCmd) {
    $ffmpegVersion = (& ffmpeg -version 2>&1 | Select-Object -First 1)
    Write-Host "FFmpeg = OK ($ffmpegVersion)" -ForegroundColor Green

    $whisperInfo = & ffmpeg -hide_banner -h filter=whisper 2>&1

    if ($LASTEXITCODE -eq 0 -and ($whisperInfo -match 'whisper')) {
        Write-Host "FFmpeg whisper filter = OK" -ForegroundColor Green
    }
    else {
        Write-Host "FFmpeg whisper filter = NOT AVAILABLE" -ForegroundColor Yellow
    }
}
else {
    Write-Host "FFmpeg = NOT FOUND" -ForegroundColor Yellow
}

$whisperModel = Join-Path $avaRoot "models\whisper\ggml-small.bin"

if (Test-Path $whisperModel) {
    Write-Host "Whisper model = OK" -ForegroundColor Green
}
else {
    Write-Host "Whisper model = NOT FOUND: $whisperModel" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "FIRST RUN SETUP COMPLETED"
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Run the system with:"
Write-Host '.\giao dien demo\run-all.ps1'
Write-Host ""
Write-Host "Use the actual ASP.NET folder name if it contains Unicode characters."
