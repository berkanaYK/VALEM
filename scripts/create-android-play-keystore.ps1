#Requires -Version 7.2
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $env:USERPROFILE '.valem-signing'),
    [string]$Alias = 'vale-play-upload',
    [switch]$UploadGitHubSecrets,
    [switch]$GeneratePasswords
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

if ($GeneratePasswords) {
    if (-not $IsWindows) { throw 'Otomatik parola saklama Windows DPAPI gerektirir.' }
    $acl = Get-Acl -LiteralPath $output
    $acl.SetAccessRuleProtection($true, $false)
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    Set-Acl -LiteralPath $output -AclObject $acl
    $storeSecure = ConvertTo-SecureString ([Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))) -AsPlainText -Force
    $keySecure = ConvertTo-SecureString ([Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))) -AsPlainText -Force
    # DPAPI encrypts these for this Windows user/machine; no plaintext password file.
    [pscustomobject]@{ StorePassword = $storeSecure; KeyPassword = $keySecure; Alias = $Alias } |
        Export-Clixml -LiteralPath (Join-Path $output 'signing-passwords.clixml')
} else {
    $storeSecure = Read-Host 'Keystore parolası (en az 16 karakter)' -AsSecureString
    $keySecure = Read-Host 'Anahtar parolası (en az 16 karakter)' -AsSecureString
}
$storePassword = [Net.NetworkCredential]::new('', $storeSecure).Password
$keyPassword = [Net.NetworkCredential]::new('', $keySecure).Password

try {
    if ($storePassword.Length -lt 16 -or $keyPassword.Length -lt 16) {
        throw 'Her iki parola da en az 16 karakter olmalıdır.'
    }

    $env:VALE_STORE_PASSWORD = $storePassword
    $env:VALE_KEY_PASSWORD = $keyPassword
    & $keytool.Source -genkeypair -v `
        -keystore $keystore -storetype JKS -storepass:env VALE_STORE_PASSWORD `
        -alias $Alias -keypass:env VALE_KEY_PASSWORD -keyalg RSA -keysize 4096 `
        -validity 10000 -dname 'CN=VALEM Android, OU=Mobile, O=VALEM, L=Antalya, C=TR'
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $keystore)) {
        throw 'Android upload keystore oluşturulamadı.'
    }

    & $keytool.Source -exportcert -rfc -keystore $keystore -storepass:env VALE_STORE_PASSWORD -alias $Alias -file (Join-Path $output 'upload-certificate.pem')
    if ($LASTEXITCODE -ne 0) { throw 'Upload sertifikası dışa aktarılamadı.' }
    & $keytool.Source -list -v -keystore $keystore -storepass:env VALE_STORE_PASSWORD -alias $Alias |
        Select-String 'SHA256:'

    if ($UploadGitHubSecrets) {
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if (-not $gh) { throw 'GitHub CLI bulunamadı. https://cli.github.com/ üzerinden kurup `gh auth login` çalıştırın.' }

        $base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($keystore))
        $base64 | & $gh.Source secret set VALE_ANDROID_KEYSTORE_B64 --repo berkanaYK/VALEM
        if ($LASTEXITCODE -ne 0) { throw 'Keystore secret yüklenemedi.' }
        $storePassword | & $gh.Source secret set VALE_ANDROID_STORE_PASSWORD --repo berkanaYK/VALEM
        if ($LASTEXITCODE -ne 0) { throw 'Store password secret yüklenemedi.' }
        $Alias | & $gh.Source secret set VALE_ANDROID_KEY_ALIAS --repo berkanaYK/VALEM
        if ($LASTEXITCODE -ne 0) { throw 'Alias secret yüklenemedi.' }
        $keyPassword | & $gh.Source secret set VALE_ANDROID_KEY_PASSWORD --repo berkanaYK/VALEM
        if ($LASTEXITCODE -ne 0) { throw 'GitHub Actions secretları tam olarak yüklenemedi.' }
        Write-Host 'Dört Android imzalama secretı GitHub Actions içine yüklendi.'
    }

    Write-Host "Kalıcı VALEM upload keystore hazır: $keystore"
    Write-Warning 'Bu dosyayı ve iki parolayı en az iki ayrı güvenli çevrimdışı konumda yedekleyin. Depoya, e-postaya veya herkese açık buluta yüklemeyin.'
}
finally {
    Remove-Item Env:\VALE_STORE_PASSWORD, Env:\VALE_KEY_PASSWORD -ErrorAction SilentlyContinue
    $storePassword = $null
    $keyPassword = $null
    $storeSecure.Dispose()
    $keySecure.Dispose()
}
