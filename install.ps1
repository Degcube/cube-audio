param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\CubeAudio')
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $Destination 'CubeAudio.exe'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'CubeAudio.lnk'
$sign = Join-Path $PSScriptRoot 'sign.ps1'

if (Get-Process CubeAudio -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) {
    throw 'CubeAudio is still running, close it first.'
}

& (Join-Path $PSScriptRoot 'CubeAudio.Apo\build.ps1')
& $sign (Join-Path $PSScriptRoot 'CubeAudio.Apo\bin\CubeAudioApo.dll')

if (Test-Path $Destination) {
    Remove-Item $Destination -Recurse
}

dotnet publish (Join-Path $PSScriptRoot 'CubeAudio\CubeAudio.csproj') -c Release -o $Destination --nologo -v q
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}

& $sign $exe

$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = $Destination
$link.Save()

Write-Host "CubeAudio installed: $exe"
