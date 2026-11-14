param(
    [switch]$Test
)

$ErrorActionPreference = 'Stop'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw 'Visual Studio not found (vswhere.exe missing).'
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw 'No Visual Studio with C++ tools found.'
}

function Build-Project([string]$Project) {
    & $msbuild $Project -nologo -verbosity:minimal -p:Configuration=Release -p:Platform=x64
    if ($LASTEXITCODE -ne 0) {
        throw "C++ build failed: $Project"
    }
}

Build-Project (Join-Path $PSScriptRoot 'CubeAudio.Apo.vcxproj')

if ($Test) {
    $tests = Join-Path $PSScriptRoot '..\CubeAudio.Apo.Test'
    Build-Project (Join-Path $tests 'CubeAudio.Apo.Test.vcxproj')

    & (Join-Path $tests 'bin\ApoTest.exe') (Join-Path $PSScriptRoot 'bin\CubeAudioApo.dll')
    if ($LASTEXITCODE -ne 0) {
        throw 'ApoTest failed.'
    }
}
