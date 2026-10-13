param([string]$UncDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$msbuild = & $vswhere -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
Push-Location $root
try {
    & $msbuild src/CreateFile.Shell/CreateFile.Shell.vcxproj /p:Configuration=Release /p:Platform=x64 /v:minimal
    if ($LASTEXITCODE) { throw 'Shell build failed.' }
    & $msbuild src/CreateFile.Shell/ShellSmoke.vcxproj /p:Configuration=Release /p:Platform=x64 /v:minimal
    if ($LASTEXITCODE) { throw 'Smoke test build failed.' }
    & ./artifacts/shell/x64/Release/ShellSmoke.exe (Join-Path $root 'artifacts/shell/x64/Release/CreateFile.Shell.dll')
    if ($LASTEXITCODE) { throw 'Native smoke test failed.' }
    $special = Join-Path $root ('artifacts/shell-test-fixtures/space & %PATH% (folder)/' + [char]0x9a8c + [char]0x8bc1)
    New-Item -ItemType Directory -Path $special -Force | Out-Null
    $targets = @($special, [IO.Path]::GetPathRoot($root))
    if ($UncDirectory) {
        if (!$UncDirectory.StartsWith('\\')) { throw '-UncDirectory must be an explicit UNC folder path.' }
        $targets += $UncDirectory
    }
    foreach ($target in $targets) {
        Write-Host "Testing shell target: $target"
        & ./artifacts/shell/x64/Release/ShellSmoke.exe (Join-Path $root 'artifacts/shell/x64/Release/CreateFile.Shell.dll') --target $target
        if ($LASTEXITCODE) { throw "Native target test failed: $target" }
    }
    if (!$UncDirectory) { Write-Host 'UNC filesystem access NOT TESTED (pass -UncDirectory with an existing test share). UNC argument quoting was tested.' }
} finally { Pop-Location }
