param([switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$testPort = 0
foreach ($candidate in 18777..18786) {
    $testUrl = "http://localhost:$candidate/"
    try {
        $identity = Invoke-RestMethod -Uri ($testUrl + 'demo-id.txt') -TimeoutSec 3
        if ($identity -is [byte[]]) { $identity = [Text.Encoding]::UTF8.GetString($identity) }
        if ($identity.Trim() -eq 'project-run-test-scenes') {
            Write-Host "Project Run test scenes are already running: $testUrl"
            if (-not $NoBrowser) { Start-Process $testUrl }
            return
        }
    } catch { }
    $probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $candidate)
    try { $probe.Start(); $testPort = $candidate } catch { } finally { $probe.Stop() }
    if ($testPort -ne 0) { break }
}
if ($testPort -eq 0) { throw 'No free local test-scene port was found (18777-18786).' }
& (Join-Path $PSScriptRoot 'Serve.ps1') -Root $PSScriptRoot -Port $testPort -NoBrowser:$NoBrowser
