param([switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$demoPort = 0
foreach ($candidate in 18767..18776) {
    $demoUrl = "http://localhost:$candidate/"
    try {
        $identity = Invoke-RestMethod -Uri ($demoUrl + 'demo-id.txt') -TimeoutSec 3
        if ($identity -is [byte[]]) { $identity = [Text.Encoding]::UTF8.GetString($identity) }
        if ($identity.Trim() -eq 'project-run-review-demo') {
            Write-Host "Project Run demo is already running: $demoUrl"
            if (-not $NoBrowser) { Start-Process $demoUrl }
            return
        }
    } catch { }
    $probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $candidate)
    try { $probe.Start(); $demoPort = $candidate } catch { } finally { $probe.Stop() }
    if ($demoPort -ne 0) { break }
}
if ($demoPort -eq 0) { throw 'No free local demo port was found (18767-18776).' }
& (Join-Path $PSScriptRoot 'Serve.ps1') -Root $PSScriptRoot -Port $demoPort -NoBrowser:$NoBrowser
