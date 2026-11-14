param(
    [Parameter(Mandatory)]
    [string[]]$Files
)

$ErrorActionPreference = 'Stop'

$config = Join-Path $PSScriptRoot 'signing.json'
if (-not (Test-Path $config)) {
    Write-Host 'No signing.json, skipping signing.'
    return
}

$unsigned = @($Files | Where-Object { (Get-AuthenticodeSignature $_).Status -ne 'Valid' })
if ($unsigned.Count -eq 0) {
    return
}

$signtool = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\*\x64\signtool.exe') | Sort-Object FullName | Select-Object -Last 1
if (-not $signtool) {
    throw 'signtool.exe not found (Windows SDK missing).'
}

$settings = Get-Content $config -Raw | ConvertFrom-Json

& $signtool.FullName sign /sha1 $settings.certificate /tr $settings.timestampServer /td sha256 /fd sha256 @unsigned
if ($LASTEXITCODE -ne 0) {
    throw 'Signing failed. Is the certificate from signing.json available right now (token plugged in, cloud signing logged in)?'
}
