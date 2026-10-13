param(
    [string]$Stage = 'artifacts/app/x64/Final',
    [switch]$RegisterModern,
    [switch]$EnableClassic,
    [string]$UncDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
$steps = New-Object 'System.Collections.Generic.List[object]'
$runDirectory = Join-Path $root ('artifacts/p0/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
function Invoke-Check([string]$Name, [scriptblock]$Action) {
    Write-Host "Checking: $Name"
    try {
        $details = (& $Action 2>&1 | Out-String).Trim()
        $steps.Add([pscustomobject]@{ Name=$Name; Status='Passed'; Details=$details })
        Write-Host "PASS $Name"
    } catch {
        $steps.Add([pscustomobject]@{ Name=$Name; Status='Failed'; Details=$_.ToString() })
        Write-Warning "FAIL ${Name}: $_"
    }
}
try {
    $Stage = (Resolve-Path -LiteralPath $Stage).Path
    $manifestPath = Join-Path $Stage 'AppxManifest.xml'
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Encoding UTF8
    $identity = [string]$manifest.Package.Identity.Name
    if ($identity -ne 'CreateFile' -or $manifest.Package.Identity.Publisher -ne 'CN=CreateFile') {
        throw 'This development verification script expects the local CreateFile / CN=CreateFile identity.'
    }
    $app = Join-Path $Stage 'CreateFile.App.exe'
    $dll = Join-Path $Stage 'CreateFile.Shell.dll'
    foreach ($file in @($app,$dll)) { if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing staged binary: $file" } }

    Invoke-Check 'Core tests, including ACL denial; no skipped tests allowed' {
        $output = & dotnet run --project tests/CreateFile.Core.Tests -c Release -- --require-no-skips 2>&1
        $code = $LASTEXITCODE
        $output
        if ($code) { throw "Core runner exited $code. Output: $($output -join [Environment]::NewLine)" }
    }
    Invoke-Check 'Native contract, argument, local-target and simulated-site tests' {
        & "$PSScriptRoot/Test-Shell.ps1" -UncDirectory $UncDirectory
    }
    Invoke-Check 'Staged DLL matches the tested native build' {
        $tested = Join-Path $root 'artifacts/shell/x64/Release/CreateFile.Shell.dll'
        if ((Get-FileHash -LiteralPath $tested).Hash -ne (Get-FileHash -LiteralPath $dll).Hash) {
            throw 'The staged DLL differs from the tested DLL. Rebuild/repackage before registration; do not replace a registered DLL in place.'
        }
        Get-FileHash -LiteralPath $dll -Algorithm SHA256
    }
    $binaryMatches = @($steps | Where-Object { $_.Name -eq 'Staged DLL matches the tested native build' -and $_.Status -eq 'Passed' }).Count -eq 1
    if ($RegisterModern) {
        Invoke-Check 'Register modern package for the current user' {
            if (!$binaryMatches) { throw 'Registration stopped because staged/native build hashes differ.' }
            $existing = @(Get-AppxPackage -Name $identity)
            if ($existing.Count -gt 0 -and @($existing | Where-Object { $_.InstallLocation -ne $Stage }).Count -gt 0) {
                throw 'A CreateFile package is registered from another location. Resolve that installation deliberately before registering this development stage.'
            }
            Add-AppxPackage -Register $manifestPath
        }
    }
    Invoke-Check 'Modern package manifest registration and installed payload' {
        $package = @(Get-AppxPackage -Name $identity)
        if ($package.Count -ne 1) { throw 'Expected one registered CreateFile package. Use -RegisterModern to register the stage on a permitted development machine.' }
        if ($package[0].Status.ToString() -ne 'Ok') { throw "Package status: $($package[0].Status)" }
        $registeredManifest = Get-AppxPackageManifest -Package $package[0].PackageFullName
        $verbs = @($registeredManifest.SelectNodes("//*[local-name()='FileExplorerContextMenus']/*[local-name()='ItemType']"))
        foreach ($type in @('Directory','Directory\Background')) {
            $entry = @($verbs | Where-Object { $_.Type -eq $type -and $_.Verb.Clsid -eq 'C419D0D4-39CA-43A8-9B43-75514BAB6B78' })
            if ($entry.Count -ne 1) { throw "Expected exactly one registered command for $type." }
        }
        $registeredDll = Join-Path $package[0].InstallLocation 'CreateFile.Shell.dll'
        if ((Get-FileHash -LiteralPath $registeredDll).Hash -ne (Get-FileHash -LiteralPath $dll).Hash) { throw 'Installed DLL differs from the supplied stage.' }
        $package[0] | Select-Object PackageFullName,InstallLocation,Status
    }
    Invoke-Check 'Registered out-of-process COM activation' {
        $package = @(Get-AppxPackage -Name $identity)
        if ($package.Count -ne 1) { throw 'Package not registered; cannot verify packaged COM activation.' }
        $registeredDll = Join-Path $package[0].InstallLocation 'CreateFile.Shell.dll'
        $output = & ./artifacts/shell/x64/Release/ShellSmoke.exe $registeredDll --registered 2>&1
        $code = $LASTEXITCODE
        $output
        if ($code) { throw "Registered COM probe exited $code. Output: $($output -join [Environment]::NewLine)" }
    }
    if ($EnableClassic) {
        Invoke-Check 'Enable or repair classic commands for the current user' {
            & "$PSScriptRoot/Classic-Integration.ps1" -ExecutablePath $app
        }
    }
    Invoke-Check 'Classic registry command values and executable paths' {
        foreach ($location in @('Directory\Background','Directory','DesktopBackground')) {
            $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Software\Classes\$location\shell\CreateFile.New\command")
            if (!$key) { throw "Classic command is absent for $location. Test modern-only menus first, then use -EnableClassic." }
            try {
                $token = if ($location -eq 'Directory') { '%1' } else { '%V' }
                $expected = '"{0}" --folder "{1}\."' -f $app,$token
                if ($key.GetValue('') -ne $expected) { throw "Unexpected or stale classic command for $location." }
                "$location : $expected"
            } finally { $key.Dispose() }
        }
    }
    & "$PSScriptRoot/Verify-Integration.ps1" | Out-File -LiteralPath (Join-Path $runDirectory 'registration.txt') -Encoding utf8
    $report = [ordered]@{
        SchemaVersion=1
        TimestampUtc=[DateTime]::UtcNow.ToString('o')
        OS=[Environment]::OSVersion.VersionString
        Architecture=$env:PROCESSOR_ARCHITECTURE
        Stage=$Stage
        AppSha256=(Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash
        ShellSha256=(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
        UncFilesystemTarget=$UncDirectory
        Checks=@($steps.ToArray())
        P0Complete=$false
        ManualExplorerVerification='Pending; automation and COM activation cannot prove menu visibility, target selection, refresh, duplicates, repair, or uninstall behavior.'
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runDirectory 'report.json') -Encoding utf8
    Write-Host "Evidence saved: $runDirectory"
    Write-Host 'Complete docs/P0-VERIFICATION.md and return report.json plus observed Explorer results.'
    if (@($steps | Where-Object Status -eq 'Failed').Count) { exit 1 }
} finally { Pop-Location }
