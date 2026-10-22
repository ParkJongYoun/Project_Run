param([string]$Root = $PSScriptRoot, [int]$Port = 18765, [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath (Join-Path $Root 'index.html'))) {
    $Root = Join-Path $PSScriptRoot '../../Run/Builds/WebGL_PlayModes'
}
if (-not (Test-Path -LiteralPath (Join-Path $Root 'index.html'))) {
    throw 'Build missing. In Unity run Tools > Project Run > Build Web - Game first.'
}
$Root = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
$listener.Start()
$url = "http://localhost:$Port/"
Write-Host "Project Run: $url"
Write-Host 'Keep this window open while playing. Close it to stop the local server.'
if (-not $NoBrowser) { Start-Process $url }
$mime = @{ '.html'='text/html; charset=utf-8'; '.js'='application/javascript'; '.wasm'='application/wasm'; '.json'='application/json'; '.css'='text/css'; '.png'='image/png'; '.mp4'='video/mp4'; '.webm'='video/webm' }
try {
    while ($true) {
        $client = $listener.AcceptTcpClient()
        try {
            $client.ReceiveTimeout = 5000
            $stream = $client.GetStream()
            $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::ASCII, $false, 1024, $true)
            $request = $reader.ReadLine()
            if (-not $request) { continue }
            $range = $null
            while ($line = $reader.ReadLine()) { if ($line -match '^Range: bytes=(\d+)-(\d*)$') { $range = @($Matches[1], $Matches[2]) } }
            $parts = $request.Split(' ')
            $relative = [Uri]::UnescapeDataString($parts[1].Split('?')[0]).TrimStart('/').Replace('/', '\')
            if (-not $relative) { $relative = 'index.html' }
            $path = [IO.Path]::GetFullPath((Join-Path $Root $relative))
            $status = '200 OK'; $payload = $null; $file = $null; $extra = ''; $start = 0L
            if ($parts[0] -notin @('GET','HEAD')) { $status='405 Method Not Allowed'; $payload=[Text.Encoding]::UTF8.GetBytes('GET and HEAD only') }
            elseif (-not $path.StartsWith($Root,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.File]::Exists($path)) { $status='404 Not Found'; $payload=[Text.Encoding]::UTF8.GetBytes('Not found') }
            else {
                $file = [IO.File]::OpenRead($path); $length = $file.Length; $end = $length-1
                if ($range) {
                    $start = [long]$range[0]
                    if ($range[1]) { $end = [Math]::Min([long]$range[1],$end) }
                    if ($start -gt $end) { $status='416 Range Not Satisfiable'; $extra="Content-Range: bytes */$length`r`n"; $payload=[byte[]]@() }
                    else { $status='206 Partial Content'; $extra="Content-Range: bytes $start-$end/$length`r`n"; $length=$end-$start+1 }
                }
            }
            $contentPath = $path
            if ($path.EndsWith('.gz', [StringComparison]::OrdinalIgnoreCase)) {
                $extra += "Content-Encoding: gzip`r`n"
                $contentPath = $path.Substring(0, $path.Length - 3)
            } elseif ($path.EndsWith('.br', [StringComparison]::OrdinalIgnoreCase)) {
                $extra += "Content-Encoding: br`r`n"
                $contentPath = $path.Substring(0, $path.Length - 3)
            }
            $type = $mime[[IO.Path]::GetExtension($contentPath)]
            if (-not $type) { $type='application/octet-stream' }
            if ($null -ne $payload) { $length=$payload.Length; $type='text/plain' }
            $header="HTTP/1.1 $status`r`nContent-Type: $type`r`nContent-Length: $length`r`nAccept-Ranges: bytes`r`nCache-Control: no-cache`r`nCross-Origin-Opener-Policy: same-origin`r`nCross-Origin-Embedder-Policy: require-corp`r`n${extra}Connection: close`r`n`r`n"
            $bytes=[Text.Encoding]::ASCII.GetBytes($header); $stream.Write($bytes,0,$bytes.Length)
            if ($parts[0] -ne 'HEAD') {
                if ($null -ne $payload) { $stream.Write($payload,0,$payload.Length) }
                else {
                    $null=$file.Seek($start,[IO.SeekOrigin]::Begin); $buffer=New-Object byte[] 65536
                    while ($length -gt 0) { $read=$file.Read($buffer,0,[int][Math]::Min($buffer.Length,$length)); if ($read -eq 0) { break }; $stream.Write($buffer,0,$read); $length-=$read }
                }
            }
        } catch { Write-Warning $_.Exception.Message }
        finally { if ($null -ne $file) { $file.Dispose(); $file=$null }; $client.Dispose() }
    }
} finally { $listener.Stop() }
