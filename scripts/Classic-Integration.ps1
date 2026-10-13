param([string]$ExecutablePath, [switch]$Remove)
$ErrorActionPreference = 'Stop'
$locations = @('Directory\Background','Directory','DesktopBackground')
if (!$Remove) {
    if (!$ExecutablePath) { throw 'Pass the absolute path to a stable CreateFile.App.exe installation.' }
    $ExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path
    if ([IO.Path]::GetFileName($ExecutablePath) -ne 'CreateFile.App.exe') { throw 'Expected CreateFile.App.exe.' }
}
foreach ($location in $locations) {
    $path = "Software\Classes\$location\shell\CreateFile.New"
    if ($Remove) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($path, $false); continue }
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($path)
    try {
        $key.SetValue('', 'Create New File')
        $key.SetValue('Icon', ('"{0}",0' -f $ExecutablePath))
        $command = $key.CreateSubKey('command')
        try {
            $token = if ($location -eq 'Directory') { '%1' } else { '%V' }
            $command.SetValue('', ('"{0}" --folder "{1}\."' -f $ExecutablePath, $token))
        } finally { $command.Dispose() }
    } finally { $key.Dispose() }
}
Write-Host 'Classic integration updated for this user. Open a new Explorer window.'
