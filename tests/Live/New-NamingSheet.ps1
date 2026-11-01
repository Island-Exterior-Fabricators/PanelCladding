# Writes a minimal "<CODE> - Naming Convention.xlsx" (a PNL sheet with the Job Code header and the
# code-table titles in row 12) through Excel COM with Visible = false, so a scratch copy of a main
# model can run the naming-convention sync on save. An existing file is replaced.
#
#   .\New-NamingSheet.ps1 -Path 'C:\...\naming\MET - Naming Convention.xlsx'
param([Parameter(Mandatory)] [string] $Path)

Remove-Item -LiteralPath $Path -ErrorAction SilentlyContinue
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
try {
    $workbook = $excel.Workbooks.Add()
    while ($workbook.Worksheets.Count -gt 1) { $workbook.Worksheets.Item($workbook.Worksheets.Count).Delete() }
    $sheet = $workbook.Worksheets.Item(1)
    $sheet.Name = 'PNL'
    $sheet.Range('D7').Value2 = 'Job Code'
    $titles = @{ E12 = 'Unit-Type'; F12 = 'Description'; H12 = 'Wall-Type'; I12 = 'Value'; J12 = 'Frame-Config'; K12 = 'Configuration' }
    foreach ($cell in $titles.Keys) { $sheet.Range($cell).Value2 = $titles[$cell] }
    $workbook.SaveAs($Path, 51)   # xlOpenXMLWorkbook
    $workbook.Close($false)
}
finally {
    $excel.Quit()
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
}
"Wrote $Path"
