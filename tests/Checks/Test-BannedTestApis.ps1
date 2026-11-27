[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$fixture = Join-Path $PSScriptRoot 'banned-api-fixture\BannedApiFixture.csproj'
$expected = @(
    'DateTime.Now', 'DateTime.UtcNow', 'DateTimeOffset.UtcNow', 'Stopwatch', 'Task.Delay(int)', 'Thread.Sleep(int)',
    'Process.WaitForExit(int)', 'WaitHandle.WaitOne(int)', 'Task.Wait(int)', 'CancellationTokenSource.CancellationTokenSource(int)'
)

$output = (& dotnet build $fixture -c Release --nologo -v:q 2>&1 | Out-String)
$buildExit = $LASTEXITCODE
$missing = @($expected | Where-Object { $output -notmatch ("RS0030: The symbol '" + [regex]::Escape($_) + "'") })
if ($buildExit -eq 0 -or $missing.Count -gt 0) {
    Write-Output $output
    throw "tests/BannedSymbols.txt did not fail the fixture build for: $($missing -join ', ') (build exit $buildExit)."
}
Write-Output "[OK] tests/BannedSymbols.txt fails a test build that uses $($expected.Count) banned time APIs."
