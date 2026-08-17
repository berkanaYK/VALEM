[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

dotnet build (Join-Path $repoRoot 'src\VALE.Api\VALE.Api.csproj') -c Release
dotnet test (Join-Path $repoRoot 'tests\VALE.Api.Tests\VALE.Api.Tests.csproj') -c Release
dotnet build (Join-Path $repoRoot 'src\VALE.Client\VALE.Client.csproj') -c Release -p:Platform=x64

Write-Host 'API, testler ve WinUI istemcisi başarıyla doğrulandı.' -ForegroundColor Green
