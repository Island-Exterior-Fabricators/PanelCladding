<#
.SYNOPSIS
Keeps client project data out of this public repository.

.DESCRIPTION
The hooks beside this script run it on every commit and push once `core.hooksPath` is `.githooks`
(tests/Run-Regression.ps1 sets it); the repository-content-guard smoke runs it on the tracked files.

  -Mode Staged    files the next commit adds or changes (pre-commit)
  -Mode Message   a commit message file (commit-msg, -MessageFile)
  -Mode Push      every commit and file a push sends that the remote does not have yet (pre-push;
                  reads the hook's standard input)
  -Mode Tracked   every tracked file in the working tree
  -Mode Commit    every file and the message of one commit (-Rev)

Rules: content-rules.json beside this script (file types, raw live-output folders, new evidence
images, company file-server paths), plus a private list of real client names, project codes and
numbers kept outside the repository: $env:PANELCLADDING_CONTENT_DENYLIST, or
%APPDATA%\PanelCladding\content-denylist.txt. Plain lines match case-insensitively; "re:" lines are
.NET regular expressions. Findings name the file, line and rule, never the matched text.

Exit codes: 0 clean, 1 blocked, 2 the guard itself failed (also blocks).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Staged', 'Message', 'Push', 'Tracked', 'Commit')]
    [string] $Mode,
    [string] $MessageFile,
    [string] $Remote = 'origin',
    [string] $Rev = 'HEAD'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$utf8 = New-Object System.Text.UTF8Encoding $false
$findings = New-Object System.Collections.Generic.List[string]

function Invoke-GitBytes([string[]] $Arguments, [string] $InputText) {
    $info = New-Object System.Diagnostics.ProcessStartInfo 'git'
    $info.Arguments = ($Arguments | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }) -join ' '
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.WorkingDirectory = (Get-Location).ProviderPath
    $process = [System.Diagnostics.Process]::Start($info)
    $buffer = New-Object System.IO.MemoryStream
    $copy = $process.StandardOutput.BaseStream.CopyToAsync($buffer)
    $errors = $process.StandardError.ReadToEndAsync()
    if ($InputText) {
        $bytes = $utf8.GetBytes($InputText)
        $process.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
    }
    $process.StandardInput.Close()
    $copy.Wait()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "git $($Arguments -join ' ') failed: $($errors.Result.Trim())" }
    return , $buffer.ToArray()
}

function Invoke-GitText([string[]] $Arguments, [string] $InputText) {
    $utf8.GetString((Invoke-GitBytes $Arguments $InputText))
}

# Lines for `git cat-file --batch[-check]`. Windows PowerShell writes a byte order mark ahead of a
# redirected standard input, so the first line is a blank one whose answer (" missing") is dropped;
# every requested object must then be found.
function Format-BatchInput([string[]] $Lines) { "`n" + (@($Lines | ForEach-Object { "$_`n" }) -join '') }

# Object id -> content, read through one `git cat-file --batch`.
function Read-Objects([string[]] $Ids) {
    $objects = @{}
    if (-not $Ids -or $Ids.Count -eq 0) { return $objects }
    $bytes = Invoke-GitBytes @('cat-file', '--batch') (Format-BatchInput $Ids)
    $position = [Array]::IndexOf($bytes, [byte]10, 0) + 1
    while ($position -lt $bytes.Length) {
        $end = [Array]::IndexOf($bytes, [byte]10, $position)
        $header = [System.Text.Encoding]::ASCII.GetString($bytes, $position, $end - $position).Split(' ')
        $position = $end + 1
        if ($header[1] -eq 'missing') { throw "git object $($header[0]) is missing." }
        $size = [int]$header[2]
        $content = New-Object byte[] $size
        [Array]::Copy($bytes, $position, $content, 0, $size)
        $objects[$header[0]] = $content
        $position += $size + 1
    }
    return $objects
}

function New-Rule([string] $Pattern, [bool] $IgnoreCase, [string] $Reason) {
    $options = [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
    if ($IgnoreCase) { $options = $options -bor [System.Text.RegularExpressions.RegexOptions]::IgnoreCase }
    [pscustomobject]@{ Regex = New-Object System.Text.RegularExpressions.Regex $Pattern, $options; Reason = $Reason }
}

function Add-Finding([string] $Where, [string] $Reason) {
    $line = "${Where}: $Reason"
    if (-not $findings.Contains($line)) { $findings.Add($line) }
}

function Test-FilePath([string] $Path, [bool] $IsNew) {
    if ($script:extensions -contains ([System.IO.Path]::GetExtension($Path))) { Add-Finding $Path 'project data file type' }
    foreach ($rule in $script:pathRules) {
        if (($IsNew -or -not $rule.NewFilesOnly) -and $rule.Regex.IsMatch($Path)) { Add-Finding $Path $rule.Reason }
    }
    foreach ($rule in $script:privateRules) {
        if ($rule.Regex.IsMatch($Path)) { Add-Finding $Path "$($rule.Reason) in the file path" }
    }
}

function Test-Text([string] $Where, [string] $Text, [bool] $WithTextRules) {
    $rules = @($script:privateRules)
    if ($WithTextRules) { $rules = @($script:textRules) + $rules }
    foreach ($rule in $rules) {
        $match = $rule.Regex.Match($Text)
        if ($match.Success) {
            $line = 1 + [regex]::Matches($Text.Substring(0, $match.Index), "`n").Count
            Add-Finding "${Where}:$line" $rule.Reason
        }
    }
}

function Test-FileContent([string] $Path, [byte[]] $Bytes) {
    if ($null -eq $Bytes -or [Array]::IndexOf($Bytes, [byte]0) -ge 0) { return }   # binary: path rules only
    $exempt = @($script:exemptions | Where-Object { $Path -eq $_ -or ($_.EndsWith('/') -and $Path.StartsWith($_, [StringComparison]::Ordinal)) }).Count -gt 0
    Test-Text $Path $utf8.GetString($Bytes) (-not $exempt)
}

function Test-CommitMessage([string] $Where, [byte[]] $CommitObject) {
    $text = $utf8.GetString($CommitObject)
    $start = $text.IndexOf("`n`n")
    if ($start -ge 0) { Test-Text $Where $text.Substring($start + 2) $true }
}

function Split-Nul([string] $Text) { @($Text.Split([char]0) | Where-Object { $_ }) }

try {
    if ($MessageFile) { $MessageFile = (Resolve-Path -LiteralPath $MessageFile).ProviderPath }
    Set-Location -LiteralPath (Invoke-GitText @('rev-parse', '--show-toplevel')).Trim()

    $rules = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'content-rules.json')) | ConvertFrom-Json
    $script:extensions = @($rules.blockedExtensions)
    $script:exemptions = @($rules.textRuleExemptions)
    $script:pathRules = @($rules.blockedPaths | ForEach-Object {
        New-Rule $_.pattern $true $_.reason | Add-Member -NotePropertyName NewFilesOnly -NotePropertyValue ([bool]$_.newFilesOnly) -PassThru
    })
    $script:textRules = @($rules.blockedText | ForEach-Object { New-Rule $_.pattern ([bool]$_.ignoreCase) $_.reason })

    $listPath = $env:PANELCLADDING_CONTENT_DENYLIST
    if (-not $listPath) { $listPath = Join-Path $env:APPDATA 'PanelCladding\content-denylist.txt' }
    $script:privateRules = @()
    if (Test-Path -LiteralPath $listPath -PathType Leaf) {
        $number = 0
        foreach ($line in [System.IO.File]::ReadAllLines($listPath)) {
            $entry = $line.Trim()
            if (-not $entry -or $entry.StartsWith('#')) { continue }
            $number++
            if ($entry.StartsWith('re:')) { $pattern = $entry.Substring(3) } else { $pattern = [regex]::Escape($entry) }
            $script:privateRules += New-Rule $pattern $true "private client list entry $number"
        }
    }
    else {
        [Console]::Error.WriteLine("content guard: no private client list at $listPath; checking file types, folders and path shapes only.")
    }

    $checked = 0
    switch ($Mode) {
        'Staged' {
            # :<old mode> <new mode> <old id> <new id> <status>\0<path>\0[<new path>\0]
            $tokens = (Invoke-GitText @('diff', '--cached', '--raw', '-z', '--no-abbrev', '-M', '--diff-filter=ACMRT')).Split([char]0)
            $staged = @()
            for ($i = 0; $i + 1 -lt $tokens.Length; ) {
                $meta = $tokens[$i].TrimStart(':').Split(' ')
                if ($meta[4] -match '^[RC]') { $path = $tokens[$i + 2]; $i += 3 } else { $path = $tokens[$i + 1]; $i += 2 }
                if ($meta[1] -eq '160000') { continue }   # submodule
                $staged += [pscustomobject]@{ Path = $path; Id = $meta[3]; IsNew = $meta[4] -ne 'R100' }
            }
            $contents = Read-Objects @($staged | ForEach-Object { $_.Id } | Select-Object -Unique)
            foreach ($file in $staged) {
                Test-FilePath $file.Path $file.IsNew
                Test-FileContent $file.Path $contents[$file.Id]
                $checked++
            }
        }
        'Message' {
            $text = [System.IO.File]::ReadAllText($MessageFile, $utf8)
            $scissors = $text.IndexOf('# ------------------------ >8 ------------------------')
            if ($scissors -ge 0) { $text = $text.Substring(0, $scissors) }
            $text = (($text -split "`r?`n") | Where-Object { -not $_.StartsWith('#') }) -join "`n"
            Test-Text 'commit message' $text $true
            $checked = 1
        }
        'Push' {
            # <local ref> <local id> <remote ref> <remote id>, one line per ref being pushed.
            $tips = @([Console]::In.ReadToEnd() -split "`r?`n" | Where-Object { $_ } | ForEach-Object { $_.Split(' ')[1] } | Where-Object { $_ -notmatch '^0+$' })
            if ($tips.Count -gt 0) {
                # What the remote already has (its remote-tracking refs) is not sent again.
                $known = Invoke-GitText @('for-each-ref', '--format=%(refname)', "refs/remotes/$Remote/")
                $exclude = if ($known.Trim()) { @('--not', "--remotes=$Remote") } else { @('--not', '--remotes') }
                $listing = @((Invoke-GitText (@('rev-list', '--objects') + $tips + $exclude)) -split "`n" | Where-Object { $_ })
                $typed = ''
                if ($listing.Count -gt 0) { $typed = Invoke-GitText @('cat-file', '--batch-check=%(objectname) %(objecttype) %(rest)') (Format-BatchInput $listing) }
                $commits = @(); $blobs = @()
                foreach ($line in ($typed -split "`n" | Select-Object -Skip 1 | Where-Object { $_ })) {
                    $parts = $line.Split(' ', 3)
                    if ($parts[1] -eq 'missing') { throw "git object $($parts[0]) is missing." }
                    if ($parts[1] -eq 'commit') { $commits += $parts[0] }
                    elseif ($parts[1] -eq 'blob') { $blobs += [pscustomobject]@{ Id = $parts[0]; Path = $parts[2] } }
                }
                $contents = Read-Objects (@($commits) + @($blobs | ForEach-Object { $_.Id }))
                foreach ($commit in $commits) { Test-CommitMessage "commit $($commit.Substring(0, 7)) message" $contents[$commit] }
                foreach ($blob in $blobs) {
                    Test-FilePath $blob.Path $true
                    Test-FileContent $blob.Path $contents[$blob.Id]
                }
                $checked = $commits.Count + $blobs.Count
            }
        }
        'Tracked' {
            foreach ($path in (Split-Nul (Invoke-GitText @('ls-files', '-z')))) {
                Test-FilePath $path $false
                $full = Join-Path (Get-Location).ProviderPath $path
                if ([System.IO.File]::Exists($full)) { Test-FileContent $path ([System.IO.File]::ReadAllBytes($full)) }
                $checked++
            }
        }
        'Commit' {
            $commit = (Invoke-GitText @('rev-parse', '--verify', "$Rev^{commit}")).Trim()
            $files = @()
            foreach ($entry in (Split-Nul (Invoke-GitText @('ls-tree', '-r', '-z', '--full-tree', $commit)))) {
                $meta, $path = $entry.Split("`t", 2)
                $parts = $meta.Split(' ')
                if ($parts[1] -eq 'blob') { $files += [pscustomobject]@{ Id = $parts[2]; Path = $path } }
            }
            $contents = Read-Objects (@($commit) + @($files | ForEach-Object { $_.Id } | Select-Object -Unique))
            Test-CommitMessage "commit $($commit.Substring(0, 7)) message" $contents[$commit]
            foreach ($file in $files) {
                Test-FilePath $file.Path $false
                Test-FileContent $file.Path $contents[$file.Id]
            }
            $checked = $files.Count
        }
    }

    if ($findings.Count -gt 0) {
        [Console]::Error.WriteLine("Blocked: client project data would enter the public repository ($($findings.Count) finding(s)).")
        foreach ($finding in $findings) { [Console]::Error.WriteLine("  $finding") }
        [Console]::Error.WriteLine('Remove it, or replace it with fictional data (ABC, XYZ). Raw live-run output stays outside the repository; see AGENTS.md.')
        exit 1
    }
    if ($Mode -in 'Tracked', 'Commit') {
        Write-Output "content guard: $checked files clean ($($script:privateRules.Count) private list entries)"
    }
    exit 0
}
catch {
    [Console]::Error.WriteLine("content guard failed: $($_.Exception.Message)")
    exit 2
}
