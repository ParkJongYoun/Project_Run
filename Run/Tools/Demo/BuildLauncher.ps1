param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$iconSource = Join-Path $projectRoot 'DataBundles/아트/실행아이콘/확정/플레이_아이콘_포링.png'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework C# compiler is unavailable.' }
if (-not (Test-Path -LiteralPath $iconSource)) { throw "Approved icon is missing: $iconSource" }
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$iconPath = Join-Path ([IO.Path]::GetTempPath()) ('ProjectRunLauncher_' + [Guid]::NewGuid().ToString('N') + '.ico')
$exePath = Join-Path $outputPath 'Play.exe'

# Convert the approved artwork into the standard Windows icon resolutions.
Add-Type -AssemblyName System.Drawing
$source = [Drawing.Bitmap]::FromFile($iconSource)
try {
    $frames = @()
    foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
        $bitmap = New-Object Drawing.Bitmap($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $memory = New-Object IO.MemoryStream
        try {
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source, (New-Object Drawing.Rectangle(0, 0, $size, $size)))
            $bitmap.Save($memory, [Drawing.Imaging.ImageFormat]::Png)
            $frames += [pscustomobject]@{ Size = $size; Bytes = $memory.ToArray() }
        } finally { $graphics.Dispose(); $bitmap.Dispose(); $memory.Dispose() }
    }
    $writer = New-Object IO.BinaryWriter([IO.File]::Create($iconPath))
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    } finally { $writer.Dispose() }

    & $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
        "/out:$exePath" "/win32icon:$iconPath" `
        /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
        (Join-Path $PSScriptRoot 'PlayLauncher.cs')
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $exePath)) { throw 'Play.exe compilation failed.' }
    # Remove the redundant icon left by earlier versions of this build tool.
    $legacyIcon = Join-Path $outputPath 'Play.ico'
    if (Test-Path -LiteralPath $legacyIcon) { Remove-Item -LiteralPath $legacyIcon }
    Write-Output "PLAY_LAUNCHER_BUILT $exePath"
} finally {
    $source.Dispose()
    if (Test-Path -LiteralPath $iconPath) { Remove-Item -LiteralPath $iconPath }
}
