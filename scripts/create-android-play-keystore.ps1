[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PWD 'VALEM-Play-Key'),
    [string]$Alias = 'vale-play-upload',
    [switch]$UploadGitHubSecrets
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$keytool = Get-Command keytool -ErrorAction SilentlyContinue
if (-not $keytool) {
    throw 'keytool bulunamadı. Java 17 JDK kurun ve JAVA_HOME/PATH ayarını doğrulayın.'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$output = (Resolve-Path $OutputDirectory).Path
$keystore = Join-Path $output 'VALEM-upload-key.jks'
if (Test-Path $keystore) {
    throw "Mevcut anahtarın üzerine yazılmadı: $keystore"
}

$storeSecure = Read-Host 'Keystore parolası (en az 16 karakter)' -AsSecureString
$keySecure = Read-Host 'Anahtar parolası (en az 16 karakter)' -AsSecureString
$storePassword = [Net.NetworkCredential]::new('', $storeSecure).Password
$keyPassword = [Net.NetworkCredential]::new('', $keySecure).Password

try {
    if ($storePassword.Length -lt 16 -or $keyPassword.Length -lt 16) {
        throw 'Her iki parola da en az 16 karakter olmalıdır.'
    }

    & $keytool.Source -genkeypair -v `
        -keystore $keystore -storetype JKS -storepass $storePassword `
        -alias $Alias -keypass $keyPassword -keyalg RSA -keysize 4096 `
        -validity 10000 -dname 'CN=VALEM Android, OU=Mobile, O=VALEM, L=Antalya, C=TR'
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $keystore)) {
        throw 'Android upload keystore oluşturulamadı.'
    }

    & $keytool.Source -list -v -keystore $keystore -storepass $storePassword -alias $Alias |
        Select-String 'SHA256:'

    if ($UploadGitHubSecrets) {
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if (-not $gh) { throw 'GitHub CLI bulunamadı. https://cli.github.com/ üzerinden kurup `gh auth login` çalıştırın.' }

        $base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($keystore))
        $base64 | & $gh.Source secret set VALE_ANDROID_KEYSTORE_B64 --repo berkanaYK/VALEM
        $storePassword | & $gh.Source secret set VALE_ANDROID_STORE_PASSWORD --repo berkanaYK/VALEM
        $Alias | & $gh.Source secret set VALE_ANDROID_KEY_ALIAS --repo berkanaYK/VALEM
        $keyPassword | & $gh.Source secret set VALE_ANDROID_KEY_PASSWORD --repo berkanaYK/VALEM
        if ($LASTEXITCODE -ne 0) { throw 'GitHub Actions secretları tam olarak yüklenemedi.' }
        Write-Host 'Dört Android imzalama secretı GitHub Actions içine yüklendi.'
    }

    Write-Host "Kalıcı VALEM upload keystore hazır: $keystore"
    Write-Warning 'Bu dosyayı ve iki parolayı en az iki ayrı güvenli çevrimdışı konumda yedekleyin. Depoya, e-postaya veya herkese açık buluta yüklemeyin.'
}
finally {
    $storePassword = $null
    $keyPassword = $null
    $storeSecure.Dispose()
    $keySecure.Dispose()
}
