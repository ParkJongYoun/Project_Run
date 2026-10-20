param([Parameter(Mandatory=$true)][string]$TestDirectory)
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath($TestDirectory)
$exe = Join-Path $testRoot 'Play.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Compile the test launcher first.' }
if ((Test-Path -LiteralPath (Join-Path $testRoot 'index.html')) -and
    [IO.File]::ReadAllText((Join-Path $testRoot 'index.html')) -ne '<html>Launcher protocol fixture</html>') {
    throw 'Use an empty test output, not a game build.'
}
New-Item -ItemType Directory -Path (Join-Path $testRoot 'Build'), (Join-Path $testRoot 'StreamingAssets') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $testRoot 'index.html'), '<html>Launcher protocol fixture</html>')
[IO.File]::WriteAllBytes((Join-Path $testRoot 'Build/probe.wasm'), [byte[]](0..255))
[IO.File]::WriteAllBytes((Join-Path $testRoot 'StreamingAssets/probe.mp4'), [byte[]](0..255))

function Request-Raw([int]$Port, [string]$Target, [string]$Method = 'GET', [string]$Headers = '') {
    $client = [Net.Sockets.TcpClient]::new()
    try {
        $client.Connect([Net.IPAddress]::Loopback, $Port)
        $client.ReceiveTimeout = 5000
        $stream = $client.GetStream()
        $request = [Text.Encoding]::ASCII.GetBytes("$Method $Target HTTP/1.1`r`nHost: 127.0.0.1`r`n${Headers}Connection: close`r`n`r`n")
        $stream.Write($request, 0, $request.Length)
        $memory = [IO.MemoryStream]::new()
        try {
            $stream.CopyTo($memory)
            $bytes = $memory.ToArray()
            $separator = -1
            for ($index = 0; $index -lt $bytes.Length - 3; $index++) {
                if ($bytes[$index] -eq 13 -and $bytes[$index+1] -eq 10 -and $bytes[$index+2] -eq 13 -and $bytes[$index+3] -eq 10) { $separator = $index; break }
            }
            if ($separator -lt 0) { throw 'HTTP header missing.' }
            $header = [Text.Encoding]::ASCII.GetString($bytes, 0, $separator)
            $body = [byte[]]::new($bytes.Length - $separator - 4)
            [Array]::Copy($bytes, $separator + 4, $body, 0, $body.Length)
            [pscustomobject]@{ Header=$header; Body=$body; Status=[int]$header.Split(' ')[1] }
        } finally { $memory.Dispose() }
    } finally { $client.Dispose() }
}
function Assert-Check([bool]$Passed, [string]$Name) {
    if (-not $Passed) { throw "FAILED: $Name" }
    $script:checks.Add($Name)
}
$checks = [Collections.Generic.List[string]]::new()
$process = $null
$blocker = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 18767)
try {
    $blocker.Start()
    $process = Start-Process -FilePath $exe -ArgumentList '--no-browser' -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $testRoot 'startup-error.log')
    $port = 18768
    $response = $null
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try { $response = Request-Raw $port '/'; break } catch {
            if ($process.HasExited) { throw "Launcher exited: $([IO.File]::ReadAllText((Join-Path $testRoot 'startup-error.log')))" }
            Start-Sleep -Milliseconds 200
        }
    }
    Assert-Check ($null -ne $response -and $response.Status -eq 200) 'Start from Korean path and fall back from occupied port'
    Assert-Check ($response.Header.Contains('Cross-Origin-Opener-Policy: same-origin') -and $response.Header.Contains('Cross-Origin-Embedder-Policy: require-corp')) 'WebGL isolation headers'
    $response = Request-Raw $port '/Build/probe.wasm'
    Assert-Check ($response.Status -eq 200 -and $response.Body.Length -eq 256 -and $response.Header.Contains('application/wasm')) 'WASM content type and complete bytes'
    $response = Request-Raw $port '/StreamingAssets/probe.mp4' 'HEAD'
    Assert-Check ($response.Status -eq 200 -and $response.Body.Length -eq 0 -and $response.Header.Contains('Content-Length: 256')) 'HEAD without response body'
    $response = Request-Raw $port '/StreamingAssets/probe.mp4' 'GET' "Range: bytes=10-19`r`n"
    Assert-Check ($response.Status -eq 206 -and $response.Body.Length -eq 10 -and $response.Body[0] -eq 10 -and $response.Header.Contains('Content-Range: bytes 10-19/256')) 'Video byte range'
    $response = Request-Raw $port '/StreamingAssets/probe.mp4' 'GET' "Range: bytes=-8`r`n"
    Assert-Check ($response.Status -eq 206 -and $response.Body.Length -eq 8 -and $response.Body[0] -eq 248) 'Suffix byte range'
    $response = Request-Raw $port '/StreamingAssets/probe.mp4' 'GET' "Range: bytes=999-`r`n"
    Assert-Check ($response.Status -eq 416 -and $response.Header.Contains('Content-Range: bytes */256')) 'Unsatisfiable range'
    foreach ($target in @('/missing', '/Play.exe', '/Build/../Play.exe', '/Build/%2e%2e/Play.exe', '/Build/../../outside.txt', '/Build/C:/Windows/win.ini')) {
        $response = Request-Raw $port $target
        Assert-Check ($response.Status -eq 404) "Reject $target"
    }
    $response = Request-Raw $port '/' 'POST'
    Assert-Check ($response.Status -eq 405) 'Reject unsupported HTTP methods'
    # Release the probe port before identity scanning so it cannot stall on the fake listener.
    $blocker.Stop()
    $duplicate = Start-Process -FilePath $exe -ArgumentList '--no-browser' -WindowStyle Hidden -PassThru
    Assert-Check ($duplicate.WaitForExit(10000) -and $duplicate.ExitCode -eq 0) 'Repeated launch reuses the correct existing server'
    $response = Request-Raw $port '/'
    Assert-Check ($response.Status -eq 200 -and -not $process.HasExited) 'Original server survives repeated launch'
    [pscustomobject]@{ Passed=$checks.Count; Checks=$checks; Exe=$exe } | ConvertTo-Json -Depth 3
} finally {
    $blocker.Stop()
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
}
