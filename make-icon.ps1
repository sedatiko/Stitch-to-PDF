#Requires -Version 5
# Generates assets\stitch.ico — a crimson tile with a stitch line and "PDF".
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assets = Join-Path $PSScriptRoot 'assets'
New-Item -ItemType Directory -Force $assets | Out-Null

$size = 64
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# background tile
$bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 178, 34, 34))
$g.FillRectangle($bg, 2, 2, $size - 4, $size - 4)

# dashed "stitch" line across the top
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 3
$pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
[System.Drawing.Point[]]$pts = @(
    (New-Object System.Drawing.Point 6, 20),
    (New-Object System.Drawing.Point 18, 10),
    (New-Object System.Drawing.Point 30, 20),
    (New-Object System.Drawing.Point 42, 10),
    (New-Object System.Drawing.Point 56, 20)
)
$g.DrawLines($pen, $pts)

# "PDF" label
$font = New-Object System.Drawing.Font 'Segoe UI', 18, ([System.Drawing.FontStyle]::Bold)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = 'Center'
$sf.LineAlignment = 'Center'
$rect = New-Object System.Drawing.RectangleF 0, 26, $size, 34
$g.DrawString('PDF', $font, [System.Drawing.Brushes]::White, $rect, $sf)
$g.Dispose()

$hicon = $bmp.GetHicon()
$ico = [System.Drawing.Icon]::FromHandle($hicon)
$path = Join-Path $assets 'stitch.ico'
$fs = [System.IO.File]::Create($path)
$ico.Save($fs)
$fs.Close()
Write-Host "Icon written to $path"
