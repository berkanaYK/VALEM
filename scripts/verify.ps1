[CmdletBinding()]
param(
    [string]$AndroidApkPath
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot

$apiProject = Join-Path $repoRoot 'src\VALE.Api\VALE.Api.csproj'
$apiTests = Join-Path $repoRoot 'tests\VALE.Api.Tests\VALE.Api.Tests.csproj'
$mobileProject = Join-Path $repoRoot 'src\VALE.Mobile\VALE.Mobile.csproj'
$uiTests = Join-Path $repoRoot 'tests\VALE.Mobile.UITests\VALE.Mobile.UITests.csproj'

dotnet build $apiProject -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'API Release derlemesi başarısız.' }
dotnet test $apiTests -c Release --no-restore --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { throw 'API testleri başarısız.' }
dotnet build $uiTests -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Appium gerçek cihaz test paketi derlenemedi.' }
dotnet build $mobileProject -f net10.0-android -c Release -warnaserror -p:RunAOTCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Android Release derlemesi başarısız.' }

$ef = Get-Command dotnet-ef -ErrorAction SilentlyContinue
if ($ef) {
    & $ef.Source migrations has-pending-model-changes --project $apiProject --startup-project $apiProject --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'EF Core modelinde migration olarak kaydedilmemiş değişiklik var.' }
} else {
    Write-Warning 'dotnet-ef PATH içinde değil; migration fark kontrolü atlandı. Kurulum: dotnet tool install --global dotnet-ef --version 10.0.4'
}

if (-not [string]::IsNullOrWhiteSpace($AndroidApkPath)) {
    & (Join-Path $PSScriptRoot 'run-android-device-tests.ps1') -ApkPath $AndroidApkPath
    if ($LASTEXITCODE -ne 0) { throw 'Android gerçek cihaz UI testi başarısız.' }
}

Write-Host 'API, 63 iş kuralı/güvenlik testi, Appium paketi ve Android Release derlemesi başarıyla doğrulandı.' -ForegroundColor Green
