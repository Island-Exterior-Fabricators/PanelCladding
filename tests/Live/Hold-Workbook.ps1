# Opens an exported PCreport workbook in Excel through COM (Visible = false), records its sheets,
# then keeps it open until the live suite writes rhino-done.txt, so the suite can export over a
# workbook that Excel holds. No window is shown and no input is sent.
#
#   .\Hold-Workbook.ps1 -EvidenceDir "$env:LOCALAPPDATA\PanelCladding\LiveEvidence\<yyMMdd>"
#
# Reads the workbook path from <EvidenceDir>\export-path.txt, writes <EvidenceDir>\excel-holding.txt
# (sheet list) once the workbook is open and <EvidenceDir>\excel-sheets.json at the end.
param(
    [Parameter(Mandatory)] [string] $EvidenceDir,
    [int] $TimeoutSeconds = 300
)

$pathFile = Join-Path $EvidenceDir 'export-path.txt'
$done = Join-Path $EvidenceDir 'rhino-done.txt'
Remove-Item -LiteralPath $pathFile, (Join-Path $EvidenceDir 'excel-holding.txt'), $done -ErrorAction SilentlyContinue
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while (-not (Test-Path $pathFile) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
$path = (Get-Content -LiteralPath $pathFile -Raw).Trim()

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
try {
    $workbook = $excel.Workbooks.Open($path)
    $sheets = @(foreach ($sheet in $workbook.Worksheets) {
        [pscustomobject]@{ name = $sheet.Name; rows = $sheet.UsedRange.Rows.Count; columns = $sheet.UsedRange.Columns.Count;
            header = @(1..[Math]::Min(8, $sheet.UsedRange.Columns.Count) | ForEach-Object { [string]$sheet.Cells.Item(1, $_).Text }) }
    })
    $summary = [pscustomobject]@{ path = $path; sheets = $sheets }
    $summary | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $EvidenceDir 'excel-sheets.json') -Encoding utf8
    ($sheets | ForEach-Object { $_.name }) -join ', ' | Set-Content -Path (Join-Path $EvidenceDir 'excel-holding.txt') -Encoding utf8
    while (-not (Test-Path $done) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    $workbook.Close($false)
}
finally {
    $excel.Quit()
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
}
"Held $path; sheets: $(($sheets | ForEach-Object { $_.name }) -join ', ')"
