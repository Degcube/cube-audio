param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\CubeAudio')
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $Destination 'CubeAudio.exe'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'CubeAudio.lnk'
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
        throw "CubeAudio is still registered in Windows, but $exe is missing. Run .\install.ps1 first, then uninstall again."
    }

    $removal = Start-Process $exe -ArgumentList '--detach-all' -Verb RunAs -Wait -PassThru
    if ($removal.ExitCode -ne 0) {
        throw 'CubeAudio could not be removed from Windows.'
    }

    $left = $system | Where-Object { Test-Path $_ }
    if ($left) {
        throw "Not everything was removed: $($left -join ', ')"
    }
}

if (Test-Path $shortcut) {
    Remove-Item $shortcut
}

if (Test-Path $Destination) {
    Remove-Item $Destination -Recurse
}

Write-Host 'CubeAudio removed.'
