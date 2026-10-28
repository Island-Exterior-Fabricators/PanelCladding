[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string[]] $Configuration = @('Debug', 'Release'),
    # Smoke folder names under tests/Smoke; wildcards are allowed.
    [string[]] $Name = @('*'),
    [switch] $List,
    [switch] $NoBuild,
    [int] $TimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $repo 'artifacts\tests'

$smokes = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Smoke') -Directory | Sort-Object Name | Where-Object {
    $folder = $_.Name
    @($Name | Where-Object { $folder -like $_ }).Count -gt 0
} | ForEach-Object {
    $projects = @(Get-ChildItem -LiteralPath $_.FullName -Filter '*.csproj' -File)
    if ($projects.Count -ne 1) { throw "Expected one project in $($_.FullName), found $($projects.Count)." }
    $assembly = $projects[0].BaseName
    if ((Get-Content -Raw -LiteralPath $projects[0].FullName) -match '<AssemblyName>([^<]+)</AssemblyName>') { $assembly = $Matches[1] }
    [pscustomobject]@{ Name = $_.Name; Project = $projects[0].FullName; Assembly = $assembly }
})
if ($smokes.Count -eq 0) { throw "No smoke folder matches: $($Name -join ', ')" }
if ($List) { $smokes | ForEach-Object { $_.Name }; return }

$failed = 0
foreach ($config in $Configuration) {
    $logs = Join-Path $artifacts "logs\$config"
    New-Item -ItemType Directory -Force -Path $logs | Out-Null
    $results = @()
    foreach ($smoke in $smokes) {
        $log = Join-Path $logs "$($smoke.Name).log"
        $started = Get-Date
        $status = 'pass'
        $exitCode = $null
        if (-not $NoBuild) {
            # Serial builds: every smoke builds src as a test-host DLL into the same output folders.
            & dotnet build $smoke.Project -c $config -m:1 --nologo -v:q *> "$log.build"
            if ($LASTEXITCODE -ne 0) { $status = 'build-failed'; $exitCode = $LASTEXITCODE }
        }
        if ($status -eq 'pass') {
            $exe = Join-Path $artifacts "bin\$config\$($smoke.Assembly).exe"
            if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Built smoke not found: $exe" }
            $info = New-Object System.Diagnostics.ProcessStartInfo $exe
            $info.WorkingDirectory = $repo
            $info.UseShellExecute = $false
            $info.RedirectStandardInput = $true
            $info.RedirectStandardOutput = $true
            $info.RedirectStandardError = $true
            $process = [System.Diagnostics.Process]::Start($info)
            $process.StandardInput.Close()
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            if ($process.WaitForExit($TimeoutSeconds * 1000)) {
                $process.WaitForExit()
                $exitCode = $process.ExitCode
                if ($exitCode -ne 0) { $status = 'failed' }
            }
            else {
                $process.Kill()
                $status = 'timeout'
            }
            Set-Content -LiteralPath $log -Encoding utf8 -Value ($stdout.Result + "`n--- stderr ---`n" + $stderr.Result)
        }
        $seconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)
        $results += [pscustomobject]@{ name = $smoke.Name; status = $status; exitCode = $exitCode; seconds = $seconds }
        Write-Output ('{0,-8} {1,-12} {2}' -f $config, $status.ToUpperInvariant(), $smoke.Name)
    }
    $results | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $artifacts "regression-$config.json") -Encoding utf8
    $configFailed = @($results | Where-Object { $_.status -ne 'pass' }).Count
    $failed += $configFailed
    Write-Output ('{0}: {1}/{2} passed; logs in {3}' -f $config, ($results.Count - $configFailed), $results.Count, $logs)
}
if ($failed -gt 0) { exit 1 }
