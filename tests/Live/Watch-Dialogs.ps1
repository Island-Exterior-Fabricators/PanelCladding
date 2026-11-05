# Answers the Win32 dialogs that a live suite opens in Rhino (Save As, overwrite confirmation,
# MessageBox). Dialogs are found with EnumWindows and read with UI Automation; a button is pressed
# with its UIA Invoke pattern or, where the dialog exposes its buttons only as panes, the WM_COMMAND
# message the button would send. No keyboard or mouse input is sent and the cursor does not move.
#
#   .\Watch-Dialogs.ps1 -ProcessId 64724 -Answers Save,Save,Yes -Log "$env:LOCALAPPDATA\PanelCladding\LiveEvidence\<yyMMdd>\export-dialogs.json"
#
# Each visible dialog that appears takes the next answer, which may list alternatives ("No|Don't
# Save"). The Save As dialog's offered file name and folder are logged before it is answered. A
# button is looked for until -ButtonWaitSeconds have passed (dialogs fill in their controls after
# they appear); then OK or Cancel is pressed instead and the mismatch logged. The script ends when
# every answer is used or after -TimeoutSeconds.
param(
    [Parameter(Mandatory)] [int] $ProcessId,
    [Parameter(Mandatory)] [string[]] $Answers,
    [Parameter(Mandatory)] [string] $Log,
    [int] $TimeoutSeconds = 300,
    [int] $ButtonWaitSeconds = 8
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public static class LiveDialogs {
    delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr hwnd, out int pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr hwnd);
    public static List<IntPtr> Find(int processId) {
        var found = new List<IntPtr>();
        EnumWindows((hwnd, l) => {
            int pid; GetWindowThreadProcessId(hwnd, out pid);
            var name = new StringBuilder(64); GetClassName(hwnd, name, 64);
            if (pid == processId && IsWindowVisible(hwnd) && name.ToString() == "#32770") found.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@
$ae = [System.Windows.Automation.AutomationElement]
$walker = [System.Windows.Automation.TreeWalker]::RawViewWalker

function Get-Elements($root) {
    $list = New-Object System.Collections.Generic.List[object]
    $stack = New-Object System.Collections.Stack
    $stack.Push(@($root, 0))
    while ($stack.Count -gt 0) {
        $item = $stack.Pop()
        $child = $walker.GetFirstChild($item[0])
        while ($child) {
            $list.Add($child)
            if ($item[1] -lt 8) { $stack.Push(@($child, $item[1] + 1)) }
            $child = $walker.GetNextSibling($child)
        }
    }
    $list
}

function Get-Button($elements, [string] $answer) {
    foreach ($candidate in $answer -split '\|') {
        $match = $elements | Where-Object {
            ($_.Current.ClassName -eq 'Button' -or $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button) -and
            ($_.Current.Name -replace '&', '') -eq $candidate } | Select-Object -First 1
        if ($match) { return $match }
    }
}

# WM_COMMAND with the button's control id, as the button itself sends when clicked. BM_CLICK is not
# used: it is ignored while the dialog is not the active window, which it often is not here.
function Press($dialogHandle, $button) {
    $pattern = $null
    if ($button.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke(); return 'Invoke' }
    $buttonHandle = [IntPtr]$button.Current.NativeWindowHandle
    $id = [LiveDialogs]::GetDlgCtrlID($buttonHandle)
    [void][LiveDialogs]::PostMessage($dialogHandle, 0x0111, [IntPtr]$id, $buttonHandle)   # WM_COMMAND, BN_CLICKED
    "WM_COMMAND $id"
}

$entries = New-Object System.Collections.Generic.List[object]
$done = @{}
$firstSeen = @{}
$index = 0
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ($index -lt $Answers.Count -and (Get-Date) -lt $deadline) {
    foreach ($handle in [LiveDialogs]::Find($ProcessId)) {
        $key = $handle.ToInt64()
        if ($done.ContainsKey($key)) { continue }
        if (-not $firstSeen.ContainsKey($key)) { $firstSeen[$key] = Get-Date }
        $dialog = $ae::FromHandle($handle)
        $elements = Get-Elements $dialog
        $answer = $Answers[$index]
        $button = Get-Button $elements $answer
        $waited = ((Get-Date) - $firstSeen[$key]).TotalSeconds
        if (-not $button -and $waited -lt $ButtonWaitSeconds) { continue }
        $entry = [ordered]@{
            time = (Get-Date).ToString('s'); title = $dialog.Current.Name; answer = $answer; waitedSeconds = [Math]::Round($waited, 1)
            texts = @($elements | Where-Object { $_.Current.ClassName -eq 'Static' -or $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Text } |
                ForEach-Object { $_.Current.Name } | Where-Object { $_ })
        }
        $nameBox = $elements | Where-Object { $_.Current.AutomationId -eq '1001' -and $_.Current.ClassName -eq 'Edit' } | Select-Object -First 1
        if ($nameBox) { $entry.fileName = $nameBox.Current.Name }
        $address = $elements | Where-Object { $_.Current.Name -like 'Address:*' } | Select-Object -First 1
        if ($address) { $entry.folder = $address.Current.Name }
        if (-not $button) {
            $entry.mismatch = "No '$answer' button after $ButtonWaitSeconds s"
            $button = (Get-Button $elements 'OK'), (Get-Button $elements 'Cancel') | Where-Object { $_ } | Select-Object -First 1
        }
        if ($button) {
            $entry.clicked = $button.Current.Name
            $entry.pressedWith = Press $handle $button
        }
        $done[$key] = $true
        $entries.Add([pscustomobject]$entry)
        $index++
        $entries.ToArray() | ConvertTo-Json -Depth 4 | Set-Content -Path $Log -Encoding utf8
        break
    }
    Start-Sleep -Milliseconds 200
}

$entries.ToArray() | ConvertTo-Json -Depth 4 | Set-Content -Path $Log -Encoding utf8
"Answered $index of $($Answers.Count) dialog(s); log: $Log"
