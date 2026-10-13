param([ValidateSet('x64','ARM64')][string]$Architecture = 'x64', [string]$Version)
# Builds artifacts/MintForExplorer-Setup-<version>-<arch>.exe: a per-user Inno Setup installer for the
# self-contained app (bundled .NET and Windows App SDK) with the classic Explorer verb.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    if (!$Version) { $Version = ([xml](Get-Content -LiteralPath 'Directory.Build.props' -Raw)).Project.PropertyGroup.Version }
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 1.2.3, got '$Version'." }
    $iscc = @(
        $env:ISCC
        (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
    if (!$iscc) { throw 'Inno Setup 6 not found. Install it (winget install JRSoftware.InnoSetup) or set $env:ISCC.' }

    $app = [IO.Path]::GetFullPath((Join-Path $root "artifacts/installer/$Architecture/app"))
    $allowed = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/installer')) + [IO.Path]::DirectorySeparatorChar
    if (!$app.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Staging directory is outside artifacts/installer.' }
    if (Test-Path -LiteralPath $app) { Remove-Item -LiteralPath $app -Recurse -Force }
    $rid = 'win-' + $Architecture.ToLowerInvariant()
    dotnet publish src/CreateFile.App -c Release -p:Platform=$Architecture -r $rid --self-contained true -p:WindowsAppSDKSelfContained=true -p:PublishReadyToRun=false -p:Version=$Version -o $app
    if ($LASTEXITCODE) { throw 'App publish failed.' }

    $output = Join-Path $root 'artifacts'
    & $iscc /Qp "/DAppVersion=$Version" "/DArch=$Architecture" "/DSourceDir=$app" "/DOutputDir=$output" installer/Mint.iss
    if ($LASTEXITCODE) { throw 'Installer compile failed.' }
    $setup = Join-Path $output "MintForExplorer-Setup-$Version-$Architecture.exe"
    $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host "Built installer: $setup"
    Write-Host "SHA256: $hash"
}
finally { Pop-Location }
