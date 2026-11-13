param([switch]$ProcessSlaOnly)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$api = $null
$web = $null
$tests = $null
$restartDirectory = $null
function Wait-Http($address, $process) {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) { throw "Service process exited before readiness at $address" }
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
    $restartDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "flowops-ci-$([Guid]::NewGuid())"
    New-Item -ItemType Directory -Path $restartDirectory | Out-Null
    $env:FLOWOPS_CI_RESTART_DIRECTORY = $restartDirectory
    $env:Testing__RequestsPerMinute = '1200'
    $api = Start-Process @processOptions -ArgumentList '-NoProfile', '-File', './infrastructure/dev.ps1', 'api'
    $webOptions = @{ FilePath = 'node'; PassThru = $true; WorkingDirectory = (Join-Path $root 'apps/web') }
    if ($processOptions.ContainsKey('WindowStyle')) { $webOptions.WindowStyle = $processOptions.WindowStyle }
    $web = Start-Process @webOptions -ArgumentList 'node_modules/next/dist/bin/next', 'dev', '--hostname', '127.0.0.1', '--port', '3000'
    Wait-Http 'http://127.0.0.1:5080/health/ready' $api
    Wait-Http 'http://127.0.0.1:3000/' $web
    $testOutput = Join-Path $restartDirectory 'playwright.stdout'
    $testErrors = Join-Path $restartDirectory 'playwright.stderr'
    $testArguments = @('-NoProfile', '-File', './infrastructure/dev.ps1', 'test')
    if ($ProcessSlaOnly) { $testArguments += @('-Grep', 'reassigns') }
    $tests = Start-Process @processOptions -ArgumentList $testArguments `
        -RedirectStandardOutput $testOutput -RedirectStandardError $testErrors
    $restarts = [ordered]@{ 'request' = 'ready'; 'process-request' = 'process-ready' }
    if ($ProcessSlaOnly) { $restarts.Remove('request') }
    $completed = @()
    while (!$tests.WaitForExit(250)) {
        foreach ($marker in $restarts.Keys) {
            if ($marker -in $completed -or !(Test-Path -LiteralPath (Join-Path $restartDirectory $marker))) { continue }
            if ($api.HasExited) { throw 'API exited before the requested restart' }
            $api.Kill($true)
            if (!$api.WaitForExit(10000)) { throw 'API process did not exit' }
            $api = Start-Process @processOptions -ArgumentList '-NoProfile', '-File', './infrastructure/dev.ps1', 'api'
            Wait-Http 'http://127.0.0.1:5080/health/ready' $api
            New-Item -ItemType File -Path (Join-Path $restartDirectory $restarts[$marker]) | Out-Null
            $completed += $marker
            Write-Output "Restarted the FlowOps API for $marker."
        }
    }
    if ($tests.ExitCode -ne 0) { throw "Playwright exited with code $($tests.ExitCode)" }
    if ($completed.Count -ne $restarts.Count) { throw 'Playwright did not exercise all requested API restart scenarios' }
} finally {
    foreach ($process in @($tests, $api, $web)) {
        if ($process -and !$process.HasExited) { $process.Kill($true) }
    }
    if ($restartDirectory) {
        foreach ($output in @($testOutput, $testErrors)) {
            if ($output -and (Test-Path -LiteralPath $output)) { Get-Content -LiteralPath $output }
        }
        foreach ($file in 'request', 'ready', 'process-request', 'process-ready', 'playwright.stdout', 'playwright.stderr') {
            $marker = Join-Path $restartDirectory $file
            if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
        }
        Remove-Item -LiteralPath $restartDirectory
    }
    Remove-Item Env:FLOWOPS_CI_RESTART_DIRECTORY -ErrorAction SilentlyContinue
    Pop-Location
}
