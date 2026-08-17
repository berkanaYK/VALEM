[CmdletBinding()]
param(
    [string]$ApiBaseUrl
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
    $ApiBaseUrl = Read-Host 'Bulutta yayınlanan VALE API adresi (örn. https://api.vale.com/)'
}

$uri = $null
if (-not [Uri]::TryCreate($ApiBaseUrl, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https') {
    throw 'API adresi geçerli bir HTTPS adresi olmalıdır.'
}

if (-not $ApiBaseUrl.EndsWith('/')) {
    $ApiBaseUrl += '/'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $repoRoot 'src\VALE.Client\appsettings.json'
$json = @{ ApiBaseUrl = $ApiBaseUrl } | ConvertTo-Json
[IO.File]::WriteAllText($settingsPath, $json, [Text.UTF8Encoding]::new($false))

Write-Host "İstemci API adresi güncellendi: $ApiBaseUrl" -ForegroundColor Green

