[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$installer = Join-Path $PSScriptRoot 'Install-PanelCladdingEditor.ps1'
if (@(Get-Process -Name Rhino -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Close every Rhino window, then run Install.cmd again.'
}
& $installer -Mode Install
& $installer -Mode Validate
Write-Output 'Installation and validation passed. Start Rhino 8 and run PCEditor.'
