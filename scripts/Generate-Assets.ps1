$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/CreateFile.App/Assets'
function Color([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
# Mint logo on a 256x256 canvas; keep in sync with assets/logo.svg.
function Draw-Logo([int]$width, [int]$height) {
    $bitmap = New-Object System.Drawing.Bitmap $width,$height
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $scale = [Math]::Min($width, $height) / 256.0
    $g.TranslateTransform([float](($width-256*$scale)/2),[float](($height-256*$scale)/2))
    $g.ScaleTransform([float]$scale,[float]$scale)
    $page = New-Object System.Drawing.Drawing2D.GraphicsPath
    $page.AddLine(68,16,160,16); $page.AddLine(160,16,216,72); $page.AddLine(216,72,216,212)
    $page.AddArc(160,184,56,56,0,90); $page.AddLine(188,240,68,240)
    $page.AddArc(40,184,56,56,90,90); $page.AddLine(40,212,40,44)
    $page.AddArc(40,16,56,56,180,90); $page.CloseFigure()
    $fold = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fold.AddLine(160,16,160,58); $fold.AddBezier(160,58,160,66,164,72,174,72); $fold.AddLine(174,72,216,72); $fold.CloseFigure()
    $leaf = New-Object System.Drawing.Drawing2D.GraphicsPath
    $leaf.AddBezier(86,196,78,138,116,104,178,100); $leaf.AddBezier(178,100,180,162,144,200,86,196); $leaf.CloseFigure()
    $body = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0,16)),([System.Drawing.PointF]::new(0,240)),(Color '#4FD1A0'),(Color '#12916C')
    $foldBrush = New-Object System.Drawing.SolidBrush (Color '#C6F5E1')
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $vein = New-Object System.Drawing.Pen (Color '#1FA57C'),9
    $vein.StartCap = $vein.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $stem = New-Object System.Drawing.Pen ([System.Drawing.Color]::White),12
    $stem.StartCap = $stem.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    try {
        $g.FillPath($body,$page); $g.FillPath($foldBrush,$fold)
        $g.FillPath($white,$leaf); $g.DrawLine($stem,90,192,74,210)
        $g.DrawLine($vein,98,184,156,124)
    } finally { $g.Dispose(); $page.Dispose(); $fold.Dispose(); $leaf.Dispose(); $body.Dispose(); $foldBrush.Dispose(); $white.Dispose(); $vein.Dispose(); $stem.Dispose() }
    return ,$bitmap
}
function Get-PngBytes([int]$width, [int]$height) {
    $bitmap = Draw-Logo $width $height
    $memory = New-Object System.IO.MemoryStream
    try { $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png); return ,$memory.ToArray() } finally { $bitmap.Dispose(); $memory.Dispose() }
}
$sizes = @{
    'Square150x150Logo.scale-200.png' = @(300,300)
    'Square44x44Logo.scale-200.png' = @(88,88)
    'Square44x44Logo.targetsize-24_altform-unplated.png' = @(24,24)
    'Square44x44Logo.targetsize-48_altform-lightunplated.png' = @(48,48)
    'StoreLogo.png' = @(50,50)
    'Wide310x150Logo.scale-200.png' = @(620,300)
    'SplashScreen.scale-200.png' = @(1240,600)
    'LockScreenLogo.scale-200.png' = @(48,48)
}
foreach ($entry in $sizes.GetEnumerator()) {
    [IO.File]::WriteAllBytes((Join-Path $assetDirectory $entry.Key), (Get-PngBytes $entry.Value[0] $entry.Value[1]))
}
# Multi-resolution ICO with PNG payloads; avoids leaking an unmanaged HICON handle.
$iconSizes = 16,20,24,32,40,48,64,256
$images = @(foreach ($size in $iconSizes) { ,(Get-PngBytes $size $size) })
$stream = [IO.File]::Create((Join-Path $assetDirectory 'AppIcon.ico'))
$writer = New-Object IO.BinaryWriter $stream
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$iconSizes.Count)
    $offset = 6 + 16 * $iconSizes.Count
    for ($i = 0; $i -lt $iconSizes.Count; $i++) {
        $dimension = [byte]($iconSizes[$i] % 256)
        $writer.Write($dimension); $writer.Write($dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image) }
} finally { $writer.Dispose(); $stream.Dispose() }