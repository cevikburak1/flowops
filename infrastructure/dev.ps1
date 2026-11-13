param(
    [Parameter(Mandatory)]
    [ValidateSet('init', 'integrations', 'api', 'web', 'test')]
    [string]$Action
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $root '.env'

if ($Action -eq 'init') {
    if (Test-Path -LiteralPath $configPath) { throw '.env already exists' }
    $entries = 'POSTGRES_PASSWORD', 'KEYCLOAK_ADMIN_PASSWORD', 'FLOWOPS_OIDC_SECRET', 'RABBITMQ_PASSWORD', 'FLOWOPS_DEV_PASSWORD', 'N8N_ENCRYPTION_KEY', 'OBJECT_STORAGE_SECRET'
    $config = foreach ($entry in $entries) {
        $bytes = New-Object byte[] 32
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        "$entry=$([BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant())"
    }
    $config | Set-Content -LiteralPath $configPath -Encoding ASCII
    return
}

if (!(Test-Path -LiteralPath $configPath)) { throw 'Run infrastructure/dev.ps1 init first' }
$config = ConvertFrom-StringData (Get-Content -Raw -LiteralPath $configPath)
$npm = if ([System.Environment]::OSVersion.Platform -eq 'Win32NT') { 'npm.cmd' } else { 'npm' }
if ($Action -eq 'integrations') {
    foreach ($entry in 'N8N_ENCRYPTION_KEY', 'OBJECT_STORAGE_SECRET') {
        if (!$config[$entry]) {
            $bytes = New-Object byte[] 32
            $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
            try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
            $config[$entry] = [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
            Add-Content -LiteralPath $configPath -Value "$entry=$($config[$entry])" -Encoding ASCII
        }
    }
    Push-Location $root
    try {
        docker compose --profile integrations up -d --wait
        exit $LASTEXITCODE
    } finally {
        Pop-Location
    }
}
if ($Action -eq 'web') {
    & $npm --prefix (Join-Path $root 'apps/web') run dev -- --hostname 127.0.0.1
    exit $LASTEXITCODE
}
if ($Action -eq 'test') {
    $env:FLOWOPS_DEV_PASSWORD = $config.FLOWOPS_DEV_PASSWORD
    & $npm --prefix (Join-Path $root 'apps/web') run test:e2e
    exit $LASTEXITCODE
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
$env:ConnectionStrings__FlowOps = "Host=localhost;Port=55432;Database=flowops;Username=flowops;Password=$($config.POSTGRES_PASSWORD)"
$env:Auth__ClientSecret = $config.FLOWOPS_OIDC_SECRET
$env:FlowOps__SeedDevelopment = 'true'
if (!$env:Processes__Storage__Endpoint -and $config.OBJECT_STORAGE_SECRET) {
    $env:Processes__Storage__Endpoint = 'http://127.0.0.1:58333'
    $env:Processes__Storage__Bucket = 'flowops-dev'
    $env:Processes__Storage__AccessKey = 'flowops-dev'
    $env:Processes__Storage__SecretKey = $config.OBJECT_STORAGE_SECRET
}
if (!$env:Integrations__RabbitMq) {
    $rabbitPassword = [Uri]::EscapeDataString($config.RABBITMQ_PASSWORD)
    $env:Integrations__RabbitMq = "amqp://flowops:${rabbitPassword}@localhost:55672"
}
$env:DOTNET_CLI_HOME = Join-Path $root '.cache/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $root '.cache/nuget'
$sdk = Join-Path $root '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk = 'dotnet' }
Push-Location (Join-Path $root 'backend')
try {
    & $sdk run --project src/FlowOps.Api --no-launch-profile
    exit $LASTEXITCODE
} finally {
    Pop-Location
}
