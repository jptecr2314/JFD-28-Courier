param([Parameter(Mandatory)][string]$GamePath,[Parameter(Mandatory)][string]$CompilerPath,[Parameter(Mandatory)][string]$BundlePath,[string]$OutputDirectory=(Join-Path $PSScriptRoot 'artifacts'))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
Copy-Item -LiteralPath $BundlePath -Destination (Join-Path $OutputDirectory 'JFD-28 Courier_0.44.0.nobp') -Force
& "$PSScriptRoot/tools/build-baked-airframe-data.ps1" -GamePath $GamePath -CompilerPath $CompilerPath
if(!$?){throw 'Data build failed'}
& "$PSScriptRoot/tools/build-baked-airframe-runtime.ps1" -GamePath $GamePath -CompilerPath $CompilerPath -OutputDirectory $OutputDirectory -CourierOnly -BalancedOnly -Version '0.44.0' -BundleFileName 'JFD-28 Courier_0.44.0.nobp'
if(!$?){throw 'Runtime build failed'}
