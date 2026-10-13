param([Parameter(Mandatory)][string]$Stage, [ValidateSet('x64','ARM64')][string]$Architecture = 'x64', [string]$Publisher = 'CN=CreateFile', [string]$IdentityName = 'CreateFile', [string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$Version) { $Version = ([xml](Get-Content -LiteralPath "$root/Directory.Build.props" -Raw)).Project.PropertyGroup.Version + '.0' }
$Stage = (Resolve-Path -LiteralPath $Stage).Path
foreach ($required in @('CreateFile.App.exe','CreateFile.Shell.dll','CreateFile.App.pri')) { if (!(Test-Path -LiteralPath (Join-Path $Stage $required))) { throw "Missing package input: $required" } }
[xml]$manifest = Get-Content -LiteralPath "$root/packaging/AppxManifest.xml"
$manifest.Package.Identity.ProcessorArchitecture = $Architecture.ToLowerInvariant()
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Package.Identity.Name = $IdentityName
$manifest.Package.Identity.Version = $Version
$manifest.Save((Join-Path $Stage 'AppxManifest.xml'))
$sdk = Get-ChildItem "${env:ProgramFiles(x86)}/Windows Kits/10/bin" -Directory | Where-Object { $_.Name -match '^10\.' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$makeappx = Join-Path $sdk.FullName 'x64/makeappx.exe'
$output = Join-Path $root "artifacts/CreateFile-$Version-$Architecture.msix"
# Keep SDK manifest/schema validation enabled. No /nv bypass.
& $makeappx pack /d $Stage /p $output /o
if ($LASTEXITCODE) { throw 'MSIX schema validation or packaging failed.' }
Write-Host "Unsigned MSIX: $output"
Write-Host 'Sign with your release certificate before distribution. The certificate subject must match Publisher.'
