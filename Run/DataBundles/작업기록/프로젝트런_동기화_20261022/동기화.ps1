$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath('C:\Project_MR').TrimEnd('\')
$targetRoot = [IO.Path]::GetFullPath('C:\Project_Run').TrimEnd('\')
if ($sourceRoot -ne 'C:\Project_MR' -or $targetRoot -ne 'C:\Project_Run' -or $sourceRoot -eq $targetRoot) { throw 'Unexpected sync paths' }
foreach ($root in @($sourceRoot, $targetRoot)) {
    if ((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked root: $root" }
    if (-not (Test-Path -LiteralPath "$root\.git" -PathType Container)) { throw "Missing independent Git: $root" }
}
$excludedRelative = @('.git', 'Run\Library', 'Run\Temp', 'Run\Logs')
$recordRelative = 'Run\DataBundles\작업기록\프로젝트런_동기화_20261022'
$workLog = Join-Path $env:TEMP ('project-run-sync-' + [guid]::NewGuid().ToString('N') + '.txt')
function Get-GitSnapshot([string]$root) {
    $gitRoot = Join-Path $root '.git'
    $items = @(Get-ChildItem -LiteralPath $gitRoot -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($gitRoot.Length + 1)
        $hash = ''
        if ($relative -notmatch '^(objects|lfs)\\') { $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        "$relative|$($_.Length)|$($_.LastWriteTimeUtc.Ticks)|$hash"
    })
    return ($items -join "`n")
}
function Get-ProjectFiles([string]$root) {
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($root)
    while ($pending.Count -gt 0) {
        $dir = $pending.Pop()
        foreach ($item in Get-ChildItem -LiteralPath $dir -Force) {
            $relative = $item.FullName.Substring($root.Length + 1)
            if ($item.Name -eq '.git' -or $excludedRelative -contains $relative) { continue }
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked project item: $($item.FullName)" }
            if ($item.PSIsContainer) { $pending.Push($item.FullName) } else { $item }
        }
    }
}
$sourceGitBefore = Get-GitSnapshot $sourceRoot
$targetGitBefore = Get-GitSnapshot $targetRoot
$excludeDirs = @('.git') + @($excludedRelative | Where-Object { $_ -ne '.git' } | ForEach-Object { Join-Path $sourceRoot $_; Join-Path $targetRoot $_ })
& robocopy $sourceRoot $targetRoot /MIR /XD $excludeDirs /XF .git /XJ /R:0 /W:0 /NP /BYTES /FP "/UNILOG:$workLog"
$copyExit = $LASTEXITCODE
if ($copyExit -ge 8) { throw "Robocopy failed: $copyExit ($workLog)" }
Write-Output "Mirror completed: $copyExit"
foreach ($relative in @('Run\Library','Run\Temp','Run\Logs')) {
    $cachePath = [IO.Path]::GetFullPath((Join-Path $targetRoot $relative))
    if (-not $cachePath.StartsWith($targetRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe cache path: $cachePath" }
    if (Test-Path -LiteralPath $cachePath) {
        if ((Get-Item -LiteralPath $cachePath).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked cache: $cachePath" }
        Remove-Item -LiteralPath $cachePath -Recurse -Force
    }
}
Write-Output 'Target Unity caches removed; checking file contents.'
$sourceFiles = @(Get-ProjectFiles $sourceRoot)
$targetFiles = @(Get-ProjectFiles $targetRoot)
$targetMap = @{}
foreach ($file in $targetFiles) { $targetMap[$file.FullName.Substring($targetRoot.Length + 1)] = $file }
$corrected = 0
$totalBytes = [long]0
foreach ($file in $sourceFiles) {
    $relative = $file.FullName.Substring($sourceRoot.Length + 1)
    if (-not $targetMap.ContainsKey($relative)) { throw "Missing target file: $relative" }
    $targetFile = $targetMap[$relative]
    $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $targetHash = (Get-FileHash -LiteralPath $targetFile.FullName -Algorithm SHA256).Hash
    if ($sourceHash -ne $targetHash) {
        Copy-Item -LiteralPath $file.FullName -Destination $targetFile.FullName -Force
        if ((Get-FileHash -LiteralPath $targetFile.FullName -Algorithm SHA256).Hash -ne $sourceHash) { throw "Content mismatch: $relative" }
        $corrected++
    }
    $totalBytes += $file.Length
    $targetMap.Remove($relative)
}
if ($targetMap.Count -gt 0) { throw "Extra target files: $($targetMap.Keys -join ', ')" }
if ($sourceGitBefore -cne (Get-GitSnapshot $sourceRoot)) { throw 'Source Git metadata changed' }
if ($targetGitBefore -cne (Get-GitSnapshot $targetRoot)) { throw 'Target Git metadata changed' }
$recordSource = Join-Path $sourceRoot $recordRelative
$recordTarget = Join-Path $targetRoot $recordRelative
Copy-Item -LiteralPath $workLog -Destination (Join-Path $recordSource '동기화_실행목록.txt') -Force
Copy-Item -LiteralPath (Join-Path $recordSource '동기화_실행목록.txt') -Destination (Join-Path $recordTarget '동기화_실행목록.txt') -Force
$report = @"
# Project_Run 동기화 결과

- 날짜: 2026-10-22 (Asia/Seoul)
- 기준: C:\Project_MR
- 대상: C:\Project_Run
- 기준에 없는 대상 파일과 폴더는 제거하고, 추가·수정 파일은 기준으로 교체했다.
- Git: 양쪽 .git 제외. 전체 Git 파일 목록·크기·수정시각과 objects/lfs 외 파일 SHA-256이 작업 전후 동일함을 확인했다. 커밋·푸시는 실행하지 않았다.
- Unity 사용 중인 Library·Temp·Logs는 복사하지 않고 대상의 해당 캐시를 제거했다. Unity에서 다시 생성된다.
- 소스 파일 수: $($sourceFiles.Count)
- 검증한 총 바이트: $totalBytes
- SHA-256: 전 파일 내용 일치. 타임스탬프 비교에서 누락된 내용 차이 $corrected 개 추가 교정.
- 대상에만 남은 파일: 0개 (Git 제외)
- Robocopy 종료 코드: $copyExit (8 미만 성공)
- 동기화 기록은 검증 후 양쪽에 동일하게 추가했다.
- Unity 실행·컴파일·빌드는 수행하지 않았다.
"@
foreach ($dir in @($recordSource, $recordTarget)) { Set-Content -LiteralPath (Join-Path $dir '동기화_결과.md') -Value $report -Encoding utf8 }
Write-Output $report
