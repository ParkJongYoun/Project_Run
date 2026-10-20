param(
    [string]$PresentationPath = 'D:\Project_Run\Run\DataBundles\작업기록\제안서_편집형PPT_20261020\편집형초안.pptx',
    [string]$RenderDirectory = 'D:\Project_Run\Run\DataBundles\작업기록\제안서_편집형PPT_20261020\파워포인트검수',
    [switch]$TestEditing
)
$ErrorActionPreference = 'Stop'
$alreadyRunning = @(Get-Process -Name POWERPNT -ErrorAction SilentlyContinue).Count -gt 0
$powerPoint = $null
$deck = $null
New-Item -ItemType Directory -Path $RenderDirectory -Force | Out-Null
try {
    $powerPoint = New-Object -ComObject PowerPoint.Application
    $deck = $powerPoint.Presentations.Open($PresentationPath, -1, 0, 0)
    $summary = @()
    foreach ($slide in $deck.Slides) {
        $nativeTexts = 0
        $nativeTables = 0
        $pictures = 0
        foreach ($shape in $slide.Shapes) {
            if ($shape.HasTextFrame -eq -1 -and $shape.TextFrame.HasText -eq -1) {
                $nativeTexts++
            }
            if ($shape.HasTable -eq -1) { $nativeTables++ }
            if ($shape.Type -eq 13) { $pictures++ }
        }
        $slide.Export((Join-Path $RenderDirectory ('슬라이드_{0:00}.png' -f $slide.SlideIndex)), 'PNG', 1280, 720)
        $summary += [pscustomobject]@{
            Slide = $slide.SlideIndex
            EditableTextBoxes = $nativeTexts
            EditableTables = $nativeTables
            IndividualPictures = $pictures
        }
    }
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $RenderDirectory '검수결과.json') -Encoding utf8
    Write-Output ($summary | ConvertTo-Json -Compress)
    if ($TestEditing) {
        # Work only on an unsaved test copy. The deliverable is opened read-only.
        $firstText = @($deck.Slides.Item(1).Shapes | Where-Object {
            $_.HasTextFrame -eq -1 -and $_.TextFrame.HasText -eq -1
        })[0]
        $firstText.TextFrame.TextRange.Text = '편집 확인'
        $tableShape = @($deck.Slides.Item(11).Shapes | Where-Object { $_.HasTable -eq -1 })[0]
        $tableShape.Table.Cell(1, 1).Shape.TextFrame.TextRange.Text = '표 편집 확인'
        $testCopy = Join-Path $RenderDirectory '편집확인용.pptx'
        $deck.SaveCopyAs($testCopy, 24)
        $deck.Close()
        $deck = $powerPoint.Presentations.Open($testCopy, -1, 0, 0)
        $textPresent = @($deck.Slides.Item(1).Shapes | Where-Object {
            $_.HasTextFrame -eq -1 -and $_.TextFrame.HasText -eq -1 -and $_.TextFrame.TextRange.Text -eq '편집 확인'
        }).Count -eq 1
        $tableShape = @($deck.Slides.Item(11).Shapes | Where-Object { $_.HasTable -eq -1 })[0]
        $tablePresent = $tableShape.Table.Cell(1, 1).Shape.TextFrame.TextRange.Text -eq '표 편집 확인'
        if (-not ($textPresent -and $tablePresent)) { throw 'Native editing persistence check failed.' }
        Write-Output 'Text and table edit/save/reopen checks passed.'
    }
} finally {
    if ($null -ne $deck) {
        $deck.Close()
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($deck)
    }
    if ($null -ne $powerPoint) {
        if (-not $alreadyRunning) { $powerPoint.Quit() }
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($powerPoint)
    }
}
