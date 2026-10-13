param([Parameter(Mandatory)][string]$PackagePath, [Parameter(Mandatory)][string]$CertificateThumbprint, [string]$TimestampUrl)
$ErrorActionPreference = 'Stop'
$sdk = Get-ChildItem "${env:ProgramFiles(x86)}/Windows Kits/10/bin" -Directory | Where-Object { $_.Name -match '^10\.' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$signTool = Join-Path $sdk.FullName 'x64/signtool.exe'
$arguments = @('sign','/fd','SHA256','/sha1',$CertificateThumbprint)
if ($TimestampUrl) { $arguments += @('/tr',$TimestampUrl,'/td','SHA256') }
$arguments += (Resolve-Path -LiteralPath $PackagePath).Path
& $signTool @arguments
if ($LASTEXITCODE) { throw 'Signing failed. Confirm the certificate subject matches the manifest Publisher.' }
& $signTool verify /pa (Resolve-Path -LiteralPath $PackagePath).Path
if ($LASTEXITCODE) { throw 'Signature trust verification failed.' }
