[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$KeyStorePath,
    [Parameter(Mandatory)][string]$StorePasswordFile,
    [Parameter(Mandatory)][string]$KeyPasswordFile,
    [string]$KeyAlias = 'vale-play-upload',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\android')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = Join-Path $PSScriptRoot '..\src\VALE.Mobile\VALE.Mobile.csproj'
foreach ($path in @($KeyStorePath, $StorePasswordFile, $KeyPasswordFile)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Signing input missing: $path" }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
foreach ($format in @('apk', 'aab')) {
    $publish = Join-Path $output $format
    dotnet publish $project -f net10.0-android -c Release -warnaserror `
        "-p:AndroidPackageFormats=$format" -p:RunAOTCompilation=false -p:AndroidKeyStore=true `
        "-p:AndroidSigningKeyStore=$KeyStorePath" "-p:AndroidSigningKeyAlias=$KeyAlias" `
        "-p:AndroidSigningStorePass=file:$StorePasswordFile" "-p:AndroidSigningKeyPass=file:$KeyPasswordFile" `
        --output $publish
    if ($LASTEXITCODE -ne 0) { throw "Signed $format publish failed." }
    $signed = @(Get-ChildItem -LiteralPath $publish -Filter "*-Signed.$format")
    if ($signed.Count -ne 1) { throw "Expected exactly one signed $format package; found $($signed.Count)." }
    Copy-Item -LiteralPath $signed[0].FullName -Destination (Join-Path $output "VALE.$format") -Force
}
# Public certificate only; the keystore and passwords must never be release assets.
keytool -exportcert -rfc -keystore $KeyStorePath -storepass:file $StorePasswordFile -alias $KeyAlias -file (Join-Path $output 'upload-certificate.pem')
if ($LASTEXITCODE -ne 0) { throw 'Public certificate export failed.' }
