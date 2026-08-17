[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:OS -ne 'Windows_NT') {
    throw 'Bu kurulum yalnızca Windows 10/11 üzerinde çalıştırılabilir.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $repoRoot 'tools\winui-config.yaml'
$clientProject = Join-Path $repoRoot 'src\VALE.Client\VALE.Client.csproj'
$apiProject = Join-Path $repoRoot 'src\VALE.Api\VALE.Api.csproj'
$testProject = Join-Path $repoRoot 'tests\VALE.Api.Tests\VALE.Api.Tests.csproj'

Write-Host 'WinUI geliştirme bileşenleri denetleniyor ve eksikler kuruluyor...'
Push-Location (Split-Path -Parent $configPath)
try {
    winget configure -f (Split-Path -Leaf $configPath) --accept-configuration-agreements --disable-interactivity
}
finally {
    Pop-Location
}

dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
$templateList = dotnet new list winui
if ($LASTEXITCODE -ne 0 -or -not ($templateList -match 'winui')) {
    throw 'WinUI dotnet şablonu doğrulanamadı.'
}

Write-Host 'Resmi WinUI şablonu geçici bir projeyle doğrulanıyor...'
$templateCheckRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("vale-winui-check-" + [Guid]::NewGuid().ToString('N'))
try {
    dotnet new winui -o $templateCheckRoot --framework net10.0 --unpackaged
    $templateProject = Get-ChildItem -LiteralPath $templateCheckRoot -Filter '*.csproj' | Select-Object -First 1
    if ($null -eq $templateProject) {
        throw 'Resmi WinUI şablonu proje dosyası üretmedi.'
    }
    dotnet build $templateProject.FullName -p:Platform=x64
    if ($LASTEXITCODE -ne 0) {
        throw 'Resmi WinUI şablonu derlenemedi.'
    }
}
finally {
    if (Test-Path -LiteralPath $templateCheckRoot) {
        Remove-Item -LiteralPath $templateCheckRoot -Recurse -Force
    }
}

dotnet dev-certs https --trust
dotnet restore $apiProject
dotnet build $apiProject -c Debug
dotnet test $testProject -c Debug
dotnet restore $clientProject -p:Platform=x64
dotnet build $clientProject -c Debug -p:Platform=x64

Write-Host ''
Write-Host 'VALE geliştirme ortamı ve proje derlemesi tamamlandı.' -ForegroundColor Green
Write-Host 'Sıradaki adım: .\scripts\configure-api.ps1'
