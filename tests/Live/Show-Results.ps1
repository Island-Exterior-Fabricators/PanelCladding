[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Path,
    [switch] $Commands,
    [int] $Width = 600
)

$ErrorActionPreference = 'Stop'

function Limit([string] $Text, [int] $Length) {
    if ($Text.Length -gt $Length) { $Text.Substring(0, $Length) } else { $Text }
}

function Format-Value($Value) {
    if ($null -eq $Value) { return 'null' }
    ConvertTo-Json -InputObject $Value -Depth 20 -Compress
}

$data = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path).Path) | ConvertFrom-Json
'{0}: {1}  passed {2}  failed {3}  {4}' -f $data.suite, $data.status, $data.passed, $data.failed, (Limit ([string] $data.error) 300)
foreach ($check in @($data.checks)) {
    $label = if ($check.pass) { 'PASS ' } else { 'FAIL ' }
    $label + $check.id + '  ' + (Limit (Format-Value $check.evidence) $Width)
}
foreach ($note in @($data.notes.PSObject.Properties)) {
    'NOTE ' + $note.Name + ': ' + (Limit (Format-Value $note.Value) $Width)
}
if ($Commands) {
    foreach ($command in @($data.commands)) {
        '>> [{0}] {1}  ({2} s)' -f $command.step, $command.macro, $command.seconds
        '   ' + (Limit (([string] $command.history).Trim() -replace "`n", "`n   ") ($Width * 2))
    }
}
