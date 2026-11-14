param(
    [Parameter(Mandatory)]
    [string]$Output
)

$ErrorActionPreference = 'Stop'

$sign = Join-Path $PSScriptRoot 'sign.ps1'

& (Join-Path $PSScriptRoot 'CubeAudio.Apo\build.ps1')
& $sign (Join-Path $PSScriptRoot 'CubeAudio.Apo\bin\CubeAudioApo.dll')

if (Test-Path $Output) {
    Remove-Item $Output -Recurse
}

dotnet publish (Join-Path $PSScriptRoot 'CubeAudio\CubeAudio.csproj') -c Release -o $Output --nologo -v q
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}

& $sign (Join-Path $Output 'CubeAudio.exe')
