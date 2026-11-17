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

# The repository is public: the hooks in .githooks refuse commits and pushes that carry client
# project data. Git must run them from this checkout's own .githooks folder, so that a branch is
# checked by its own rules. Enable them for this clone and its worktrees when unset. The Claude
# desktop app sets core.hooksPath in each worktree's config.worktree to the main checkout's absolute
# .githooks path; point such a worktree back at its own folder. Leave any other hooks folder alone.
function Get-RepoFullPath([string] $Path) {
    [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($repo, $Path)).TrimEnd('\', '/')
}
$hooksPath = & git -C $repo config --get core.hooksPath
if (-not $hooksPath) {
    & git -C $repo config core.hooksPath .githooks
    Write-Output 'Enabled the repository hooks (core.hooksPath = .githooks).'
}
else {
    # The folder git really uses; a relative path is relative to the working tree root.
    $hooksFolder = Get-RepoFullPath (& git -C $repo rev-parse --git-path hooks)
    if ($hooksFolder -ne (Get-RepoFullPath '.githooks')) {
        $scope = ((& git -C $repo config --show-scope --get core.hooksPath) -split "`t")[0]
        $checkoutHooks = @(& git -C $repo worktree list --porcelain | Where-Object { $_ -like 'worktree *' } |
            ForEach-Object { Get-RepoFullPath ($_.Substring(9) + '/.githooks') })
        if ($scope -eq 'worktree' -and $checkoutHooks -contains $hooksFolder) {
            & git -C $repo config --worktree core.hooksPath .githooks
            if ($LASTEXITCODE -ne 0) { throw "Could not set core.hooksPath for this worktree." }
            Write-Output "Pointed this worktree's hooks at its own .githooks (core.hooksPath was '$hooksPath')."
        }
        else {
            Write-Warning "core.hooksPath is '$hooksPath', so this checkout's client data hooks in .githooks do not run."
        }
    }
}

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
