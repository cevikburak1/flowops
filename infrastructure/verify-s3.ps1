$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$config = ConvertFrom-StringData (Get-Content -Raw -LiteralPath (Join-Path $root '.env'))
$secret = $config.OBJECT_STORAGE_SECRET
if ([string]::IsNullOrWhiteSpace($secret)) {
    throw 'Run infrastructure/dev.ps1 integrations to configure the local storage secret.'
}

$curl = if ($IsWindows) { 'curl.exe' } else { 'curl' }
$nullDevice = if ($IsWindows) { 'NUL' } else { '/dev/null' }
$endpoint = "http://127.0.0.1:58333/flowops-dev/verification/$([Guid]::NewGuid()).txt"
$payload = "FlowOps storage verification $([Guid]::NewGuid())"
$curlConfig = 'user = "flowops-dev:{0}"' -f $secret.Replace('\', '\\').Replace('"', '\"')

function Invoke-SignedRequest([string[]] $Options) {
    # Keep the secret out of curl's command line.
    $result = $curlConfig | & $curl --disable --config - --aws-sigv4 'aws:amz:us-east-1:s3' --silent --show-error --fail --max-time 10 @Options $endpoint
    if ($LASTEXITCODE -ne 0) { throw "Signed S3 request failed with curl exit code $LASTEXITCODE" }
    return $result
}

$written = $false
try {
    Invoke-SignedRequest @('--request', 'PUT', '--data-binary', $payload) | Out-Null
    $written = $true
    if ((Invoke-SignedRequest @()) -cne $payload) { throw 'Signed S3 read did not match the payload.' }
    $unsignedStatus = & $curl --disable --silent --show-error --max-time 10 --output $nullDevice --write-out '%{http_code}' $endpoint
    if ($LASTEXITCODE -ne 0 -or $unsignedStatus -notin @('401', '403')) {
        throw "Unsigned S3 read returned HTTP $unsignedStatus."
    }
    Write-Output 'Signed S3 write and read passed. Unsigned access was rejected.'
} finally {
    if ($written) { Invoke-SignedRequest @('--request', 'DELETE') | Out-Null }
}
