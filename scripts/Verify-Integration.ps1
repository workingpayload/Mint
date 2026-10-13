param([string]$IdentityName = 'CreateFile')
$ErrorActionPreference = 'Stop'
$package = Get-AppxPackage -Name $IdentityName
if (!$package) { Write-Warning 'No package identity registered. The modern command cannot be activated through MSIX.' }
else {
    $package | Select-Object Name,PackageFullName,Architecture,InstallLocation,Status
    $manifest = Get-AppxPackageManifest -Package $package.PackageFullName
    $manifest.SelectNodes("//*[local-name()='FileExplorerContextMenus' or local-name()='SurrogateServer']") | ForEach-Object { $_.OuterXml }
    $dll = Join-Path $package.InstallLocation 'CreateFile.Shell.dll'
    if (!(Test-Path -LiteralPath $dll)) { throw 'Registered shell DLL is missing.' }
}
foreach ($location in @('Directory\Background','Directory','DesktopBackground')) {
    $path = "Software\Classes\$location\shell\CreateFile.New\command"
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($path)
    if ($key) { try { Write-Host "$location : $($key.GetValue(''))" } finally { $key.Dispose() } }
    else { Write-Host "$location : classic entry absent" }
}
Write-Host 'Registration is not proof of Explorer behavior. Complete docs/VERIFICATION.md on the target Windows build.'
