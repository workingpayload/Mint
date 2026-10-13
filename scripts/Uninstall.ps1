param([string]$IdentityName = 'CreateFile')
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/Classic-Integration.ps1" -Remove
Get-AppxPackage -Name $IdentityName | Remove-AppxPackage
Write-Host 'Package and classic entries removed. User settings are retained.'
