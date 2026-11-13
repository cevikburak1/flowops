$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$api = $null
$web = $null
function Wait-Http($address) {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            if ((Invoke-WebRequest -Uri $address -TimeoutSec 10).StatusCode -eq 200) { return }
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    throw "Service unavailable at $address"
}

Push-Location $root
try {
    $processOptions = @{ FilePath = 'pwsh'; PassThru = $true }
    if ([System.Environment]::OSVersion.Platform -eq 'Win32NT') { $processOptions.WindowStyle = 'Hidden' }
    $api = Start-Process @processOptions -ArgumentList '-NoProfile', '-File', './infrastructure/dev.ps1', 'api'
    $web = Start-Process @processOptions -ArgumentList '-NoProfile', '-File', './infrastructure/dev.ps1', 'web'
    Wait-Http 'http://127.0.0.1:5080/health/ready'
    Wait-Http 'http://127.0.0.1:3000/'
    ./infrastructure/dev.ps1 test
    if ($LASTEXITCODE -ne 0) { throw "Playwright exited with code $LASTEXITCODE" }
} finally {
    foreach ($process in @($api, $web)) {
        if ($process -and !$process.HasExited) { $process.Kill($true) }
    }
    Pop-Location
}
