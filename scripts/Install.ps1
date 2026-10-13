param([Parameter(Mandatory)][string]$PackagePath, [string[]]$DependencyPath)
$ErrorActionPreference = 'Stop'
$arguments = @{ Path = (Resolve-Path -LiteralPath $PackagePath).Path }
if ($DependencyPath) { $arguments.DependencyPath = $DependencyPath }
Add-AppxPackage @arguments
Write-Host 'Installed for the current user. Open a new Explorer window and verify the context menu.'
