[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [string]$AndroidSdkDirectory = $env:ANDROID_HOME
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $AndroidSdkDirectory) { $AndroidSdkDirectory = $env:ANDROID_SDK_ROOT }
if (-not $AndroidSdkDirectory) { throw 'Android SDK directory is required.' }
$buildTools = Get-ChildItem -LiteralPath (Join-Path $AndroidSdkDirectory 'build-tools') -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $buildTools) { throw 'Android build-tools missing.' }
$apk = Join-Path $PackageDirectory 'VALE.apk'
$aab = Join-Path $PackageDirectory 'VALE.aab'
$cert = [Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([IO.File]::ReadAllText((Join-Path $PackageDirectory 'upload-certificate.pem')))
$expected = $cert.GetCertHashString([Security.Cryptography.HashAlgorithmName]::SHA256).ToLowerInvariant()
$signature = & (Join-Path $buildTools.FullName 'apksigner.bat') verify --verbose --print-certs $apk 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $signature -notmatch [regex]::Escape($expected)) { throw 'APK signature verification or signing certificate match failed.' }
if ($signature -match 'Android Debug' -or $signature -notmatch 'Verified using v2 scheme.*true') { throw 'APK must use a release certificate and APK signature scheme v2.' }
Write-Host $signature
& (Join-Path $buildTools.FullName 'zipalign.exe') -c -P 16 4 $apk
if ($LASTEXITCODE -ne 0) { throw 'APK 16 KB ZIP alignment failed.' }
$manifest = & (Join-Path $buildTools.FullName 'aapt2.exe') dump xmltree --file AndroidManifest.xml $apk 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { throw 'APK manifest could not be inspected.' }
foreach ($attribute in @('allowBackup', 'usesCleartextTraffic')) {
    if ($manifest -notmatch "$attribute.*(?:false|0x0(?:\s|$))") { throw "APK must disable $attribute." }
}
if ($manifest -match 'debuggable.*(?:true|0xffffffff)') { throw 'APK is debuggable.' }
$badging = & (Join-Path $buildTools.FullName 'aapt2.exe') dump badging $apk 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $badging -notmatch "name='com.berkanayk.vale'") { throw 'Unexpected Android application ID.' }
if ($badging -notmatch "targetSdkVersion:'(\d+)'" -or [int]$Matches[1] -lt 35) { throw 'Android target SDK must be at least 35.' }

$jarVerification = & jarsigner '-J-Duser.language=en' -verify $aab 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $jarVerification -notmatch 'jar verified\.') { throw 'AAB JAR signature verification failed.' }
$aabCertificate = & keytool '-J-Duser.language=en' -printcert -jarfile $aab 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or ($aabCertificate.Replace(':', '').ToLowerInvariant()) -notmatch [regex]::Escape($expected)) { throw 'AAB upload certificate differs from APK certificate.' }

# Inspect native ELF LOAD segment alignment in both deliverables.
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($package in @($apk, $aab)) {
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $libraries = @($archive.Entries | Where-Object { $_.FullName -match '(^|/)lib/(arm64-v8a|x86_64)/.*\.so$' })
        if ($libraries.Count -eq 0) { throw '64-bit native libraries missing.' }
        foreach ($entry in $libraries) {
            $stream = $entry.Open()
            $memory = [IO.MemoryStream]::new()
            try { $stream.CopyTo($memory); $bytes = $memory.ToArray() } finally { $stream.Dispose(); $memory.Dispose() }
            if ($bytes.Length -lt 64 -or $bytes[0] -ne 127 -or $bytes[1] -ne 69 -or $bytes[2] -ne 76 -or $bytes[3] -ne 70 -or $bytes[4] -ne 2 -or $bytes[5] -ne 1) { throw "Unsupported ELF: $($entry.FullName)" }
            $offset = [BitConverter]::ToUInt64($bytes, 32)
            $size = [BitConverter]::ToUInt16($bytes, 54)
            $count = [BitConverter]::ToUInt16($bytes, 56)
            for ($i = 0; $i -lt $count; $i++) {
                $header = [int]($offset + $i * $size)
                if ([BitConverter]::ToUInt32($bytes, $header) -eq 1 -and [BitConverter]::ToUInt64($bytes, $header + 48) -lt 16384) {
                    throw "ELF LOAD segment is not 16 KB aligned: $($entry.FullName)"
                }
            }
        }
        Write-Host "$([IO.Path]::GetFileName($package)): $($libraries.Count) native libraries passed 16 KB alignment."
    } finally { $archive.Dispose() }
}
@('VALE.apk', 'VALE.aab', 'upload-certificate.pem') | ForEach-Object {
    $hash = Get-FileHash -LiteralPath (Join-Path $PackageDirectory $_) -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $_"
} | Set-Content -LiteralPath (Join-Path $PackageDirectory 'SHA256SUMS.txt') -Encoding utf8
Write-Host "APK/AAB signature, manifest, identity and native alignment verified. Certificate SHA-256: $expected"
