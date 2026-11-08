param([string]$MobileRoot)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$brandingRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/assets/branding'))
$masterPath = Join-Path $brandingRoot 'pick-and-roll-hoop-badge-master.png'
$master = [Drawing.Image]::FromFile($masterPath)
$navy = [Drawing.Color]::FromArgb(7, 19, 31)

function New-BrandBitmap([int]$Size, [double]$Scale, [bool]$Opaque) {
    $format = if ($Opaque) { [Drawing.Imaging.PixelFormat]::Format24bppRgb } else { [Drawing.Imaging.PixelFormat]::Format32bppArgb }
    $bitmap = [Drawing.Bitmap]::new($Size, $Size, $format)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear($(if ($Opaque) { $navy } else { [Drawing.Color]::Transparent }))
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $edge = [single]($Size * $Scale)
        $offset = [single](($Size - $edge) / 2)
        $graphics.DrawImage($master, [Drawing.RectangleF]::new($offset, $offset, $edge, $edge))
    } finally { $graphics.Dispose() }
    return ,$bitmap
}

function Save-BrandPng([string]$Path, [int]$Size, [double]$Scale = 1, [bool]$Opaque = $false) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    $bitmap = New-BrandBitmap $Size $Scale $Opaque
    try { $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose() }
}

function Save-WindowsIcon([string]$Path) {
    $sizes = @(16, 32, 48, 256)
    $images = foreach ($size in $sizes) {
        $bitmap = New-BrandBitmap $size .94 $true
        $stream = [IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            ,$stream.ToArray()
        } finally { $bitmap.Dispose(); $stream.Dispose() }
    }
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
            $offset += $images[$i].Length
        }
        foreach ($image in $images) { $writer.Write([byte[]]$image) }
        $writer.Flush()
        [IO.File]::WriteAllBytes($Path, $stream.ToArray())
    } finally { $writer.Dispose(); $stream.Dispose() }
}

try {
    $svg = [IO.File]::ReadAllText((Join-Path $brandingRoot 'pick-and-roll-hoop-badge.svg'))
    $faviconSvg = [regex]::Replace($svg, '(<svg[^>]*>)', '$1<rect width="256" height="256" rx="48" fill="#07131F"/>')
    [IO.File]::WriteAllText((Join-Path $brandingRoot 'hoop-badge-favicon.svg'), $faviconSvg)
    Save-BrandPng (Join-Path $brandingRoot 'hoop-badge-favicon-32.png') 32 1 $true
    Save-BrandPng (Join-Path $brandingRoot 'hoop-badge-favicon-64.png') 64 1 $true
    Save-BrandPng (Join-Path $brandingRoot 'hoop-badge-apple-touch.png') 180 .94 $true
    Save-BrandPng (Join-Path $brandingRoot 'hoop-badge-app-icon-1024.png') 1024 .94 $true

    if ($MobileRoot) {
        $mobilePath = (Resolve-Path -LiteralPath $MobileRoot).Path
        if (!(Test-Path -LiteralPath (Join-Path $mobilePath 'pubspec.yaml'))) { throw 'MobileRoot must point to the Flutter project.' }
        $mobileBrand = Join-Path $mobilePath 'assets/branding'
        [IO.Directory]::CreateDirectory($mobileBrand) | Out-Null
        Copy-Item -LiteralPath (Join-Path $brandingRoot 'pick-and-roll-hoop-badge.svg') -Destination $mobileBrand
        Save-BrandPng (Join-Path $mobileBrand 'pick-and-roll-hoop-badge.png') 512
        Save-BrandPng (Join-Path $mobileBrand 'app-icon-1024.png') 1024 .94 $true
        # Keep the complete badge inside Android's circular adaptive-icon safe zone.
        Save-BrandPng (Join-Path $mobileBrand 'android-adaptive-foreground.png') 432 .58

        $densities = @(@('mdpi', 1), @('hdpi', 1.5), @('xhdpi', 2), @('xxhdpi', 3), @('xxxhdpi', 4))
        foreach ($density in $densities) {
            $resourceRoot = Join-Path $mobilePath 'android/app/src/main/res'
            $mipmap = Join-Path $resourceRoot ('mipmap-' + $density[0])
            Save-BrandPng (Join-Path $mipmap 'ic_launcher.png') ([int](48 * $density[1])) .94 $true
            Save-BrandPng (Join-Path $mipmap 'ic_launcher_foreground.png') ([int](108 * $density[1])) .58
            $drawable = Join-Path $resourceRoot ('drawable-' + $density[0])
            Save-BrandPng (Join-Path $drawable 'pick_and_roll_splash.png') ([int](112 * $density[1]))
        }

        foreach ($platform in @('ios', 'macos')) {
            $iconRoot = Join-Path $mobilePath "$platform/Runner/Assets.xcassets/AppIcon.appiconset"
            $catalog = Get-Content -LiteralPath (Join-Path $iconRoot 'Contents.json') -Raw | ConvertFrom-Json
            foreach ($entry in $catalog.images) {
                $points = [double]::Parse($entry.size.Split('x')[0], [Globalization.CultureInfo]::InvariantCulture)
                $scale = [int]$entry.scale.TrimEnd('x')
                Save-BrandPng (Join-Path $iconRoot $entry.filename) ([int]($points * $scale)) .94 $true
            }
        }
        $launchRoot = Join-Path $mobilePath 'ios/Runner/Assets.xcassets/LaunchImage.imageset'
        Save-BrandPng (Join-Path $launchRoot 'LaunchImage.png') 112
        Save-BrandPng (Join-Path $launchRoot 'LaunchImage@2x.png') 224
        Save-BrandPng (Join-Path $launchRoot 'LaunchImage@3x.png') 336

        $webRoot = Join-Path $mobilePath 'web'
        Save-BrandPng (Join-Path $webRoot 'favicon.png') 64 1 $true
        foreach ($size in @(192, 512)) {
            Save-BrandPng (Join-Path $webRoot "icons/Icon-$size.png") $size .94 $true
            Save-BrandPng (Join-Path $webRoot "icons/Icon-maskable-$size.png") $size .74 $true
        }
        Save-WindowsIcon (Join-Path $mobilePath 'windows/runner/resources/app_icon.ico')
        Write-Output 'Exported Flutter branding, Android adaptive/legacy icons, Apple icons, splash images, web icons, and Windows ICO.'
    }
    Write-Output 'Exported portal favicons, Apple touch icon, and 1024px app icon.'
} finally { $master.Dispose() }
