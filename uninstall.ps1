#Requires -Version 5
# Removes the "Stitch into PDF" context-menu entry and the installed files.
# -ResetMultiSelectLimit also removes the Explorer MultipleInvokePromptMinimum override
# if install.ps1 -RaiseMultiSelectLimit was used.
[CmdletBinding()]
param([switch]$ResetMultiSelectLimit)

$ErrorActionPreference = 'Stop'

foreach ($t in @('SystemFileAssociations\image', 'SystemFileAssociations\.pdf')) {
    $key = "HKCU:\Software\Classes\$t\shell\StitchPDF"
    if (Test-Path $key) { Remove-Item $key -Recurse -Force }
}

$appDir = Join-Path $env:LOCALAPPDATA 'Programs\StitchPDF'
if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }

if ($ResetMultiSelectLimit) {
    $explorer = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer'
    if (Get-ItemProperty -Path $explorer -Name 'MultipleInvokePromptMinimum' -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $explorer -Name 'MultipleInvokePromptMinimum'
    }
}

Write-Host 'StitchPDF uninstalled.' -ForegroundColor Green
