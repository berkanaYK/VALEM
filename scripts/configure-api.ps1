[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot 'src\VALE.Api\VALE.Api.csproj'

function Read-SecretText([string]$Prompt) {
    $secureValue = Read-Host $Prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

$connectionString = Read-SecretText 'Bulut PostgreSQL bağlantı dizesi'
$adminEmail = Read-Host 'İlk yönetici e-posta adresi'
$adminPassword = Read-SecretText 'İlk yönetici parolası (en az 10 karakter, büyük/küçük harf, rakam ve sembol)'
$adminName = Read-Host 'İlk yöneticinin adı soyadı'
$branchName = Read-Host 'İlk şubenin adı (örn. Lara Şubesi)'
$branchCode = Read-Host 'İlk şubenin kısa kodu (örn. LARA)'
$branchCity = Read-Host 'Şehir'

$jwtBytes = New-Object byte[] 64
[Security.Cryptography.RandomNumberGenerator]::Fill($jwtBytes)
$jwtKey = [Convert]::ToBase64String($jwtBytes)

dotnet user-secrets set 'ConnectionStrings:ValeDatabase' $connectionString --project $apiProject
dotnet user-secrets set 'Jwt:Key' $jwtKey --project $apiProject
dotnet user-secrets set 'Seed:AdminEmail' $adminEmail --project $apiProject
dotnet user-secrets set 'Seed:AdminPassword' $adminPassword --project $apiProject
dotnet user-secrets set 'Seed:AdminFullName' $adminName --project $apiProject
dotnet user-secrets set 'Seed:DefaultBranchName' $branchName --project $apiProject
dotnet user-secrets set 'Seed:DefaultBranchCode' $branchCode --project $apiProject
dotnet user-secrets set 'Seed:DefaultBranchCity' $branchCity --project $apiProject

$connectionString = $null
$adminPassword = $null
$jwtKey = $null
[GC]::Collect()

Write-Host 'API sırları güvenli geliştirme deposuna kaydedildi.' -ForegroundColor Green
Write-Host 'API başlatmak için: .\scripts\run-api.ps1'

