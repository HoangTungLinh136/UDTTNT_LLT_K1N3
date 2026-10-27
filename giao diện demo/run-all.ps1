$ErrorActionPreference = "Stop"

# Child dotnet processes inherit these settings. They avoid background workload
# advertising work that can crash the Windows CLI after a successful build.
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = "true"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "true"
$env:DOTNET_SDK_VULNERABILITY_CHECK_DISABLE = "true"
$env:DOTNET_NOLOGO = "true"

# ============================================================
# PORTABLE PATHS
# ============================================================

$webRoot = $PSScriptRoot
$releaseRoot = Split-Path $webRoot -Parent
$avaRoot = Join-Path $releaseRoot "ava_service"

$py = Join-Path $avaRoot ".venv\Scripts\python.exe"
$envFile = Join-Path $avaRoot ".env"

$logRoot = Join-Path $releaseRoot "data"

New-Item `
    -ItemType Directory `
    -Path $logRoot `
    -Force |
Out-Null

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "HRMS + AVA PORTABLE STARTER"
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "ROOT : $releaseRoot"
Write-Host "AVA  : $avaRoot"
Write-Host "WEB  : $webRoot"
Write-Host ""

# ============================================================
# VALIDATE FILES
# ============================================================

if (-not (Test-Path $avaRoot)) {
    throw "Khong tim thay ava_service: $avaRoot"
}

if (-not (Test-Path $py)) {
    throw @"
Khong tim thay Python virtual environment.

Hay chay setup lan dau:
    cd "$avaRoot"
    py -3.11 -m venv .venv
    .\.venv\Scripts\python.exe -m pip install -r requirements.txt
"@
}

if (-not (Test-Path $envFile)) {
    $example = Join-Path $avaRoot ".env.example"

    if (-not (Test-Path $example)) {
        throw "Khong tim thay .env.example"
    }

    Copy-Item $example $envFile

    Write-Host ""
    Write-Host "Da tao .env tu .env.example." -ForegroundColor Yellow
    Write-Host "Hay sua AVA_INTERNAL_SECRET trong:" -ForegroundColor Yellow
    Write-Host $envFile -ForegroundColor Yellow
    Write-Host ""
    throw "Can cau hinh .env truoc lan chay dau."
}

$csproj = Get-ChildItem `
    $webRoot `
    -Filter *.csproj `
    -File |
Select-Object -First 1

if (-not $csproj) {
    throw "Khong tim thay ASP.NET .csproj trong $webRoot"
}

# ============================================================
# LOAD AVA .ENV FOR ASP.NET INTERNAL SECRET
# ============================================================

$config = @{}

foreach ($line in Get-Content $envFile -Encoding UTF8) {

    $value = $line.Trim()

    if (
        -not $value -or
        $value.StartsWith("#") -or
        -not $value.Contains("=")
    ) {
        continue
    }

    $parts = $value.Split("=", 2)

    $key = $parts[0].Trim()
    $val = $parts[1].Trim()

    $config[$key] = $val
}

if (
    -not $config.ContainsKey("AVA_INTERNAL_SECRET") -or
    [string]::IsNullOrWhiteSpace(
        $config["AVA_INTERNAL_SECRET"]
    ) -or
    $config["AVA_INTERNAL_SECRET"] -like "CHANGE_ME*"
) {
    throw "AVA_INTERNAL_SECRET chua duoc cau hinh trong ava_service\.env"
}

# ASP.NET IConfiguration:
# Ava__InternalSecret -> Ava:InternalSecret
$env:Ava__InternalSecret = $config["AVA_INTERNAL_SECRET"]

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:AVA_ENVIRONMENT = "development"
$env:PYTHONPATH = $avaRoot
$env:PYTHONIOENCODING = "utf-8"

# ============================================================
# AVA :8001
# ============================================================

$avaListener = Get-NetTCPConnection `
    -LocalPort 8001 `
    -State Listen `
    -ErrorAction SilentlyContinue |
Select-Object -First 1

if (-not $avaListener) {

    Write-Host "Starting AVA :8001 ..." -ForegroundColor Cyan

    Start-Process `
        -FilePath $py `
        -ArgumentList @(
            "-m",
            "uvicorn",
            "api.main:app",
            "--host",
            "127.0.0.1",
            "--port",
            "8001"
        ) `
        -WorkingDirectory $avaRoot `
        -RedirectStandardOutput (
            Join-Path $logRoot "ava-final.stdout.log"
        ) `
        -RedirectStandardError (
            Join-Path $logRoot "ava-final.stderr.log"
        ) `
        -WindowStyle Hidden

} else {

    Write-Host `
        "AVA 8001 already running. PID $($avaListener.OwningProcess)" `
        -ForegroundColor Green
}

# ============================================================
# ASP.NET :5249
# ============================================================

$webListener = Get-NetTCPConnection `
    -LocalPort 5249 `
    -State Listen `
    -ErrorAction SilentlyContinue |
Select-Object -First 1

if (-not $webListener) {

    Write-Host "Starting ASP.NET :5249 ..." -ForegroundColor Cyan

    $dotnetArgs = "run --project `"$($csproj.FullName)`" --urls `"http://127.0.0.1:5249`""

    Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $dotnetArgs `
        -WorkingDirectory $webRoot `
        -RedirectStandardOutput (
            Join-Path $logRoot "web-final.stdout.log"
        ) `
        -RedirectStandardError (
            Join-Path $logRoot "web-final.stderr.log"
        ) `
        -WindowStyle Hidden

} else {

    Write-Host `
        "ASP.NET 5249 already running. PID $($webListener.OwningProcess)" `
        -ForegroundColor Green
}

# ============================================================
# WAIT
# ============================================================

Write-Host ""
Write-Host "Waiting for services..." -ForegroundColor Cyan

$avaReady = $false
$avaFullyOperational = $false
$avaUnavailableCapabilities = @()
$webReady = $false

for ($i = 1; $i -le 60; $i++) {

    Start-Sleep -Seconds 1

    if (-not $avaReady) {
        try {
            $health = Invoke-RestMethod `
                "http://127.0.0.1:8001/health" `
                -TimeoutSec 2

            if ($health.status -eq "ready") {
                $avaReady = $true
                $avaFullyOperational = ($health.fullyOperational -eq $true)
                $avaUnavailableCapabilities = @($health.unavailableCapabilities)
            }
        }
        catch {}
    }

    if (-not $webReady) {
        try {
            $response = Invoke-WebRequest `
                "http://127.0.0.1:5249" `
                -UseBasicParsing `
                -TimeoutSec 2

            if ($response.StatusCode -ge 200) {
                $webReady = $true
            }
        }
        catch {}
    }

    if ($avaReady -and $webReady) {
        break
    }
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "SYSTEM STATUS"
Write-Host "======================================" -ForegroundColor Cyan

Write-Host "AVA 8001 =" $avaReady `
    -ForegroundColor $(if ($avaReady) { "Green" } else { "Red" })

Write-Host "APP 5249 =" $webReady `
    -ForegroundColor $(if ($webReady) { "Green" } else { "Red" })

if ($avaReady) {
    Write-Host "AI multimodal =" $avaFullyOperational `
        -ForegroundColor $(if ($avaFullyOperational) { "Green" } else { "Yellow" })
    if ($avaUnavailableCapabilities.Count -gt 0) {
        Write-Host "Unavailable AI capabilities:" ($avaUnavailableCapabilities -join ", ") `
            -ForegroundColor Yellow
    }
}

if ($avaReady -and $webReady) {

    Write-Host ""
    if ($avaFullyOperational) {
        Write-Host "SYSTEM READY" -ForegroundColor Green
    }
    else {
        Write-Host "WEB READY - AI CAPABILITIES DEGRADED" -ForegroundColor Yellow
    }

    Start-Process "http://127.0.0.1:5249"

} else {

    Write-Host ""
    Write-Host "SYSTEM NOT READY" -ForegroundColor Red
    Write-Host ""
    Write-Host "Logs:"
    Write-Host (Join-Path $logRoot "ava-final.stderr.log")
    Write-Host (Join-Path $logRoot "web-final.stderr.log")
}
