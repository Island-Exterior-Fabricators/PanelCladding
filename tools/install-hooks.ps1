[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

function Get-RepoFullPath([string] $Path) {
    [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($repo, $Path)).TrimEnd('\', '/')
}

function Invoke-GitConfig([string[]] $Arguments) {
    $saved = $env:GIT_CONFIG_PARAMETERS
    try {
        Remove-Item Env:GIT_CONFIG_PARAMETERS -ErrorAction SilentlyContinue
        & git -C $repo config @Arguments
    }
    finally {
        if ($null -ne $saved) { $env:GIT_CONFIG_PARAMETERS = $saved }
    }
}

$hooksPath = Invoke-GitConfig @('--get', 'core.hooksPath')
if (-not $hooksPath) {
    Invoke-GitConfig @('core.hooksPath', '.githooks')
    if ($LASTEXITCODE -ne 0) { throw 'Could not set core.hooksPath.' }
    Write-Output 'Enabled the repository hooks (core.hooksPath = .githooks): pre-commit and commit-msg run the client data guard, pre-push runs it and tools/check.ps1.'
    return
}

$hooksFolder = Get-RepoFullPath $hooksPath
if ($hooksFolder -eq (Get-RepoFullPath '.githooks')) {
    Write-Output 'The repository hooks are enabled (core.hooksPath = .githooks).'
    return
}

$scope = ((Invoke-GitConfig @('--show-scope', '--get', 'core.hooksPath')) -split "`t")[0]
$checkoutHooks = @(& git -C $repo worktree list --porcelain | Where-Object { $_ -like 'worktree *' } |
    ForEach-Object { Get-RepoFullPath ($_.Substring(9) + '/.githooks') })
if ($scope -eq 'worktree' -and $checkoutHooks -contains $hooksFolder) {
    Invoke-GitConfig @('--worktree', 'core.hooksPath', '.githooks')
    if ($LASTEXITCODE -ne 0) { throw 'Could not set core.hooksPath for this worktree.' }
    Write-Output "Pointed this worktree's hooks at its own .githooks (core.hooksPath was '$hooksPath')."
    return
}

Write-Warning "core.hooksPath is '$hooksPath', so this checkout's hooks in .githooks do not run. Set it with: git config core.hooksPath .githooks"
