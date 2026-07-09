#Requires -Version 5
# Installs StitchPDF to %LOCALAPPDATA%\Programs\StitchPDF and adds the
# "Stitch into PDF" context-menu entry for all image files and PDFs (current user only,
# no admin rights needed).
#
# On Windows 11 the entry appears under "Show more options" (or Shift + right-click).
#
# -RaiseMultiSelectLimit: Windows hides context-menu verbs when more than 15 items are
# selected. This switch raises that Explorer-wide limit to 100 (HKCU\...\Explorer\
# MultipleInvokePromptMinimum). Optional; affects all verbs, not just this one.
[CmdletBinding()]
param(
    [string]$SourceDir = (Join-Path $PSScriptRoot 'dist'),
    [switch]$RaiseMultiSelectLimit
)

$ErrorActionPreference = 'Stop'

$exeSrc = Join-Path $SourceDir 'StitchPDF.exe'
if (-not (Test-Path $exeSrc)) { throw "StitchPDF.exe not found in $SourceDir. Run build.ps1 first." }

$appDir = Join-Path $env:LOCALAPPDATA 'Programs\StitchPDF'
New-Item -ItemType Directory -Force $appDir | Out-Null
Copy-Item $exeSrc, (Join-Path $SourceDir 'PdfSharp.dll') -Destination $appDir -Force
$exe = Join-Path $appDir 'StitchPDF.exe'

# 'SystemFileAssociations\image' covers every extension Windows considers an image
# (jpg, jpeg, png, gif, bmp, tif, tiff, ...) in one key.
$targets = @('SystemFileAssociations\image', 'SystemFileAssociations\.pdf')
foreach ($t in $targets) {
    $key = "HKCU:\Software\Classes\$t\shell\StitchPDF"
    New-Item -Path "$key\command" -Force | Out-Null
    Set-ItemProperty -Path $key -Name '(Default)' -Value 'Stitch into PDF'
    Set-ItemProperty -Path $key -Name 'Icon' -Value "`"$exe`",0"
    Set-ItemProperty -Path "$key\command" -Name '(Default)' -Value "`"$exe`" `"%1`""
}

if ($RaiseMultiSelectLimit) {
    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer' `
        -Name 'MultipleInvokePromptMinimum' -Type DWord -Value 100
    Write-Host 'Raised the Explorer multi-select verb limit to 100 items.'
}

Write-Host "Installed to $appDir" -ForegroundColor Green
Write-Host 'Right-click image/PDF files -> "Show more options" -> "Stitch into PDF".'
Write-Host '(No Explorer restart needed; the menu is read live from the registry.)'
