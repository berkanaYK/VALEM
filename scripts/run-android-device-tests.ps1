[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ApkPath,
    [string]$DeviceId,
    [string]$AppiumUrl = 'http://127.0.0.1:4723',
    [switch]$UseRunningAppium
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$apk = (Resolve-Path $ApkPath).Path
if (-not $apk.EndsWith('.apk', [StringComparison]::OrdinalIgnoreCase)) {
    throw "APK yolu .apk dosyası olmalıdır: $apk"
}

foreach ($commandName in @('adb', 'dotnet')) {
    if (-not (Get-Command $commandName -ErrorAction SilentlyContinue)) {
        throw "$commandName bulunamadı. Android SDK platform-tools ve .NET 10 PATH içinde olmalıdır."
    }
}

$deviceLines = @(& adb devices | Where-Object { $_ -match '^\S+\s+device$' })
if ([string]::IsNullOrWhiteSpace($DeviceId)) {
    if ($deviceLines.Count -ne 1) {
        throw "Tam olarak bir yetkili Android cihazı bekleniyor; bulunan: $($deviceLines.Count). 'adb devices' çıktısını ve USB hata ayıklama onayını kontrol edin."
    }
    $DeviceId = ($deviceLines[0] -split '\s+')[0]
} elseif (-not ($deviceLines | Where-Object { ($_ -split '\s+')[0] -eq $DeviceId })) {
    throw "'$DeviceId' adb tarafından yetkili ve çevrimiçi cihaz olarak görülmüyor."
}

$isEmulator = (& adb -s $DeviceId shell getprop ro.kernel.qemu 2>$null | Out-String).Trim()
if ($DeviceId.StartsWith('emulator-', [StringComparison]::OrdinalIgnoreCase) -or $isEmulator -eq '1') {
    throw 'Bu komut gerçek cihaz kalite kapısıdır; emülatör algılandı.'
}

$model = (& adb -s $DeviceId shell getprop ro.product.model | Out-String).Trim()
$androidVersion = (& adb -s $DeviceId shell getprop ro.build.version.release | Out-String).Trim()
Write-Host "Gerçek cihaz hazır: $model / Android $androidVersion / $DeviceId"

$uri = [Uri]$AppiumUrl
$appiumProcess = $null
$artifactDirectory = Join-Path $PSScriptRoot '..\TestResults\android-real-device'
$artifactDirectory = [IO.Path]::GetFullPath($artifactDirectory)
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

try {
    if (-not $UseRunningAppium) {
        $appium = Get-Command appium -ErrorAction SilentlyContinue
        if (-not $appium) {
            throw 'Appium bulunamadı. Önce `npm install --global appium` ve `appium driver install uiautomator2` çalıştırın.'
        }
        $drivers = (& appium driver list --installed 2>&1 | Out-String)
        if ($LASTEXITCODE -ne 0 -or $drivers -notmatch 'uiautomator2') {
            throw 'Appium UiAutomator2 sürücüsü kurulu değil. `appium driver install uiautomator2` çalıştırın.'
        }
        if ($uri.Host -notin @('127.0.0.1', 'localhost')) {
            throw 'Appium otomatik başlatılırken yalnızca yerel adres kullanılabilir. Uzak sunucu için -UseRunningAppium seçin.'
        }

        $stdout = Join-Path $artifactDirectory 'appium.stdout.log'
        $stderr = Join-Path $artifactDirectory 'appium.stderr.log'
        $appiumProcess = Start-Process -FilePath $appium.Source -ArgumentList @('--address', $uri.Host, '--port', "$($uri.Port)") `
            -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    }

    $serverReady = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        try {
            $status = Invoke-RestMethod -Uri ([Uri]::new($uri, '/status')) -TimeoutSec 2
            if ($status.value.ready -eq $true) {
                $serverReady = $true
                break
            }
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    if (-not $serverReady) {
        throw "Appium sunucusu hazır olmadı: $AppiumUrl/status"
    }

    $env:VALE_APK_PATH = $apk
    $env:VALE_ANDROID_UDID = $DeviceId
    $env:VALE_APPIUM_SERVER_URL = $AppiumUrl
    $env:VALE_UI_ARTIFACT_DIR = $artifactDirectory

    $testProject = Join-Path $PSScriptRoot '..\tests\VALE.Mobile.UITests\VALE.Mobile.UITests.csproj'
    & dotnet restore $testProject
    if ($LASTEXITCODE -ne 0) { throw 'UI test paketi geri yüklenemedi.' }

    & dotnet test $testProject -c Release --no-restore --logger "trx;LogFileName=android-real-device.trx" `
        --results-directory $artifactDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Android gerçek cihaz UI testi başarısız.' }

    Write-Host "Android gerçek cihaz UI testi başarılı. Kanıtlar: $artifactDirectory"
} finally {
    if ($appiumProcess -and -not $appiumProcess.HasExited) {
        Stop-Process -Id $appiumProcess.Id -Force
    }
}
