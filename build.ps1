#Requires -Version 5
# Builds dist\StitchPDF.exe using the C# compiler that ships with Windows (.NET Framework 4.x).
# No Visual Studio or .NET SDK required.
[CmdletBinding()]
param([switch]$SkipIcon)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$pkg  = Join-Path $root 'packages'
New-Item -ItemType Directory -Force $dist, $pkg | Out-Null

# --- 1. PDFsharp (GDI+ build, targets .NET Framework — runs on stock Windows 10/11) ---
$pdfSharpDll = Join-Path $dist 'PdfSharp.dll'
if (-not (Test-Path $pdfSharpDll)) {
    $zip = Join-Path $pkg 'PDFsharp.1.50.5147.zip'
    if (-not (Test-Path $zip)) {
        Write-Host 'Downloading PDFsharp 1.50.5147 from nuget.org...'
        Invoke-WebRequest 'https://www.nuget.org/api/v2/package/PDFsharp/1.50.5147' -OutFile $zip
    }
    $extract = Join-Path $pkg 'PDFsharp'
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive $zip -DestinationPath $extract
    Copy-Item (Join-Path $extract 'lib\net20\PdfSharp.dll') $pdfSharpDll
}

# --- 2. App icon (generated, best-effort) ---
$icon = Join-Path $root 'assets\stitch.ico'
if (-not $SkipIcon -and -not (Test-Path $icon)) {
    try { & (Join-Path $root 'make-icon.ps1') }
    catch { Write-Warning "Icon generation failed ($_); building without an icon." }
}

# --- 3. Compile ---
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'Could not find the .NET Framework C# compiler (csc.exe).' }

$cscArgs = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+',
    "/out:$dist\StitchPDF.exe",
    "/reference:$pdfSharpDll",
    '/reference:Microsoft.VisualBasic.dll',
    "/win32manifest:$root\src\app.manifest"
)
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon" }
$cscArgs += (Get-ChildItem "$root\src\*.cs" | ForEach-Object FullName)

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }

Write-Host "Built: $dist\StitchPDF.exe" -ForegroundColor Green
