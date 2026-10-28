$ErrorActionPreference = 'Stop'
$env:PATH = 'C:\Program Files\Rhino 8\System;' + $env:PATH
$pcpointArtifacts = Join-Path $PSScriptRoot '..\..\..\artifacts\tests'
$pcpointDll = Join-Path $pcpointArtifacts 'bin\Debug\PanelCladdingPointOrderSmoke.dll'
$pcpointOutput = Join-Path $pcpointArtifacts 'panel-cladding-point-order'
New-Item -ItemType Directory -Force -Path $pcpointOutput | Out-Null
$pcpointLog = Join-Path $pcpointOutput 'native-host.log'
$pcpointProcess = Start-Process -FilePath 'C:\Program Files\dotnet\dotnet.exe' -ArgumentList @(('"' + $pcpointDll + '"'), '--native') -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $pcpointLog -RedirectStandardError (Join-Path $pcpointOutput 'native-host-error.log')
[ordered]@{ exitCode = $pcpointProcess.ExitCode; hostProcessId = $PID; finishedUtc = [DateTime]::UtcNow.ToString('O') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $pcpointOutput 'native-host-result.log') -Encoding utf8
