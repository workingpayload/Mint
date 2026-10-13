param([ValidateSet('x64','ARM64')][string]$Architecture = 'x64', [ValidateSet('Debug','Release')][string]$Configuration = 'Release', [switch]$Package, [switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $msbuild = & $vswhere -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (!$msbuild) { throw 'Install Visual Studio C++ build tools with MSVC v143 and a Windows 10/11 SDK.' }
    & $msbuild src/CreateFile.Shell/CreateFile.Shell.vcxproj /p:Configuration=$Configuration /p:Platform=$Architecture /v:minimal
    if ($LASTEXITCODE) { throw 'Native build failed.' }
    dotnet run --project tests/CreateFile.Core.Tests -c $Configuration
    if ($LASTEXITCODE) { throw 'Core tests failed.' }
    dotnet build src/CreateFile.Cli -c $Configuration
    if ($LASTEXITCODE) { throw 'CLI build failed.' }
    $rid = 'win-' + $Architecture.ToLowerInvariant()
    $stage = Join-Path $root "artifacts/app/$Architecture/$Configuration"
    $stage = [IO.Path]::GetFullPath($stage)
    $allowed = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/app')) + [IO.Path]::DirectorySeparatorChar
    if (!$stage.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Staging directory is outside artifacts/app.' }
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    $selfContained = (!$FrameworkDependent).ToString().ToLowerInvariant()
    dotnet publish src/CreateFile.App -c $Configuration -p:Platform=$Architecture -r $rid --self-contained $selfContained -p:PublishReadyToRun=false -o $stage
    if ($LASTEXITCODE) { throw 'App publish failed.' }
    Copy-Item -LiteralPath "artifacts/shell/$Architecture/$Configuration/CreateFile.Shell.dll" -Destination $stage
    if ($Package) { & "$PSScriptRoot/Package.ps1" -Stage $stage -Architecture $Architecture }
    Write-Host "Built application: $stage/CreateFile.App.exe"
}
finally { Pop-Location }
