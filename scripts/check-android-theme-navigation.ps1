param(
    [Parameter(Mandatory)][string]$DeviceSerial,
    [string]$ArtifactDirectory = './artifacts/theme-navigation',
    [string]$AdbPath = "$env:LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe"
)
# Start with the authenticated demo open and its coach marks dismissed.
# This changes local appearance only; it never clicks account/profile Save.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$null = New-Item -ItemType Directory -Path $ArtifactDirectory -Force
$ArtifactDirectory = (Resolve-Path $ArtifactDirectory).Path
$package = 'com.berkanayk.vale'
function Read-Ui {
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        & $AdbPath -s $DeviceSerial shell rm -f /sdcard/valem-theme-test.xml
        $dump = (& $AdbPath -s $DeviceSerial shell uiautomator dump /sdcard/valem-theme-test.xml 2>&1) -join "`n"
        if ($LASTEXITCODE -eq 0 -and $dump -match 'dumped to' -and $dump -notmatch 'ERROR') {
            & $AdbPath -s $DeviceSerial pull /sdcard/valem-theme-test.xml "$ArtifactDirectory/current.xml" 2>$null | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'UI XML download failed' }
            [xml]$doc = Get-Content "$ArtifactDirectory/current.xml"
            return $doc.SelectNodes('//node')
        }
        Start-Sleep -Milliseconds 250
    }
    throw 'No fresh UI snapshot; stale snapshots are never reused.'
}
function Tap-Node($node) {
    if (!$node) { throw 'UI target missing' }
    $bounds = [regex]::Match($node.bounds, '\[(\d+),(\d+)\]\[(\d+),(\d+)\]')
    $x = [int](([int]$bounds.Groups[1].Value + [int]$bounds.Groups[3].Value) / 2)
    $y = [int](([int]$bounds.Groups[2].Value + [int]$bounds.Groups[4].Value) / 2)
    & $AdbPath -s $DeviceSerial shell input tap $x $y
    Start-Sleep -Milliseconds 300
}
function Tap-Field([string]$selector) {
    $nodes = Read-Ui
    Tap-Node ($nodes | Where-Object { $_.text -eq $selector -or $_.'resource-id' -eq "$package`:id/$selector" } | Select-Object -First 1)
}
function Tap-Tab([string]$title) {
    $nodes = Read-Ui
    Tap-Node ($nodes | Where-Object { ($_.text -replace '\s+', ' ') -eq $title -and $_.'resource-id' -match 'navigation_bar_item_(small|large)_label_view$' } | Select-Object -First 1)
}
function Assert-Theme([string]$screen, [bool]$dark) {
    $nodes = Read-Ui
    $icons = $nodes | Where-Object { $_.'resource-id' -match 'navigation_bar_item_icon_view$' }
    foreach ($label in ($nodes | Where-Object { $_.'resource-id' -match 'navigation_bar_item_(small|large)_label_view$' })) {
        $bounds = [regex]::Match($label.bounds, '\[(\d+),(\d+)\]\[(\d+),(\d+)\]')
        $center = ([int]$bounds.Groups[1].Value + [int]$bounds.Groups[3].Value) / 2
        $icon = $icons | Where-Object {
            $box = [regex]::Match($_.bounds, '\[(\d+),(\d+)\]\[(\d+),(\d+)\]')
            [Math]::Abs((([int]$box.Groups[1].Value + [int]$box.Groups[3].Value) / 2) - $center) -lt 8
        } | Select-Object -First 1
        if (!$icon) { throw "${screen}: navigation icon missing" }
        $iconBox = [regex]::Match($icon.bounds, '\[(\d+),(\d+)\]\[(\d+),(\d+)\]')
        if ([int]$bounds.Groups[2].Value -lt [int]$iconBox.Groups[4].Value) {
            throw "${screen}: navigation text overlaps its icon ($($label.text))"
        }
    }
    & $AdbPath -s $DeviceSerial shell screencap -p /sdcard/valem-theme-test.png
    $path = "$ArtifactDirectory/$screen.png"
    & $AdbPath -s $DeviceSerial pull /sdcard/valem-theme-test.png $path 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Screenshot download failed' }
    $bitmap = [System.Drawing.Bitmap]::new($path)
    try {
        $pixel = $bitmap.GetPixel(8, [int]($bitmap.Height * 0.23))
        $brightness = ($pixel.R + $pixel.G + $pixel.B) / 3
        if (($dark -and $brightness -gt 80) -or (!$dark -and $brightness -lt 200)) {
            throw "${screen}: appearance changed unexpectedly ($($pixel.R),$($pixel.G),$($pixel.B))"
        }
        "$screen passed: RGB $($pixel.R),$($pixel.G),$($pixel.B)"
    }
    finally { $bitmap.Dispose() }
}
function Open-Settings { Tap-Tab 'Daha Fazla'; Tap-Field 'more-settings' }
foreach ($choice in @('Koyu', 'Açık / Beyaz')) {
    $dark = $choice -eq 'Koyu'
    $prefix = if ($dark) { 'dark' } else { 'light' }
    Open-Settings
    Tap-Field 'settings-theme-mode'; Tap-Field $choice
    Assert-Theme "$prefix-settings" $dark
    Tap-Field 'Profil, E-posta ve Temalar'
    Start-Sleep -Milliseconds 800
    Assert-Theme "$prefix-profile" $dark
    foreach ($tab in @('Ana Sayfa', 'Araçlar', 'Raporlar', 'Bildirim', 'Daha Fazla')) {
        Tap-Tab $tab
        Assert-Theme "$prefix-tab-$($tab.Replace(' ', '-'))" $dark
    }
    Open-Settings
    $selected = (Read-Ui | Where-Object { $_.'resource-id' -eq "$package`:id/settings-theme-mode" } | Select-Object -First 1).text
    if ($selected -ne $choice) { throw "Stale mode picker: expected $choice, found $selected" }
    Assert-Theme "$prefix-settings-return" $dark
}
'Theme navigation checks passed for both modes without saving to the account.'
