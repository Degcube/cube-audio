param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\CubeAudio')
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $Destination 'CubeAudio.exe'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'CubeAudio.lnk'

if (Get-Process CubeAudio -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) {
    throw 'CubeAudio is still running, close it first.'
}

& (Join-Path $PSScriptRoot 'publish.ps1') -Output $Destination

$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = $Destination
$link.Save()

Write-Host "CubeAudio installed: $exe"
