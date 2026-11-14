[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Destination = $(if (Test-Path (Join-Path $PSScriptRoot 'CubeAudio.exe')) { $PSScriptRoot } else { Join-Path $env:LOCALAPPDATA 'Programs\CubeAudio' })
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $Destination 'CubeAudio.exe'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'CubeAudio.lnk'
$files = 'CubeAudio.exe', 'LICENSE', 'THIRD-PARTY-NOTICES.txt', 'uninstall.ps1'
$system = @(
    'HKLM:\SOFTWARE\CubeAudio'
    (Join-Path $env:ProgramFiles 'CubeAudio')
    (Join-Path $env:ProgramData 'CubeAudio')
)

if (Get-Process CubeAudio -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) {
    throw 'CubeAudio is still running, close it first.'
}

if ($system | Where-Object { Test-Path $_ }) {
    if (-not (Test-Path $exe)) {
        throw "CubeAudio is still registered in Windows, but $exe is missing. Put CubeAudio.exe back there, then uninstall again."
    }

    if ($PSCmdlet.ShouldProcess('CubeAudio in Windows (boost on all microphones, effect DLL, settings, log)', 'Remove')) {
        $removal = Start-Process $exe -ArgumentList '--detach-all' -Verb RunAs -Wait -PassThru
        if ($removal.ExitCode -ne 0) {
            throw 'CubeAudio could not be removed from Windows.'
        }

        $left = $system | Where-Object { Test-Path $_ }
        if ($left) {
            throw "Not everything was removed: $($left -join ', ')"
        }
    }
}

if ((Test-Path $shortcut) -and (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut).TargetPath -eq $exe) {
    Remove-Item $shortcut
}

foreach ($file in $files) {
    $path = Join-Path $Destination $file
    if (Test-Path $path) {
        Remove-Item $path
    }
}

if ((Test-Path $Destination) -and -not (Get-ChildItem $Destination -Force)) {
    Remove-Item $Destination -ErrorAction SilentlyContinue
}

if (-not $WhatIfPreference) {
    Write-Host 'CubeAudio removed.'
}
