$ErrorActionPreference = 'Stop'

if (git -C $PSScriptRoot status --porcelain) {
    throw 'There are uncommitted changes. Commit first, so the release matches a commit.'
}

[xml]$props = Get-Content (Join-Path $PSScriptRoot 'Directory.Build.props')
$name = "CubeAudio-$($props.Project.PropertyGroup.Version)-win-x64"
$artifacts = Join-Path $PSScriptRoot 'artifacts'
$stage = Join-Path $artifacts $name
$zip = "$stage.zip"

& (Join-Path $PSScriptRoot 'publish.ps1') -Output $stage

if ((Get-AuthenticodeSignature (Join-Path $stage 'CubeAudio.exe')).Status -ne 'Valid') {
    throw 'CubeAudio.exe is not signed. A release needs signing.json.'
}

Copy-Item (Join-Path $PSScriptRoot 'LICENSE'), (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') $stage

if (Test-Path $zip) {
    Remove-Item $zip
}

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip

Write-Host "Release package: $zip"
